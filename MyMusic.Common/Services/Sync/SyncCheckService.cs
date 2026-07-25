using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// A single client-reported file descriptor for a sync check request. Mirrors the server-side
/// fields of <c>SyncFileInfoItem</c> but lives in <see cref="MyMusic.Common"/> so the service
/// has no dependency on the Server DTO layer.
/// </summary>
public record SyncCheckFileInfo
{
    public required string Path { get; init; }

    public required DateTime ModifiedAt { get; init; }

    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Input for a sync check operation. Mirrors <c>SyncCheckRequest</c> without the DTO dependency.
/// </summary>
public record SyncCheckInput
{
    public required List<SyncCheckFileInfo> Files { get; init; }

    public bool Force { get; init; }
}

/// <summary>
/// Result of a sync check operation. The controller maps this to <c>SyncCheckResponse</c>.
/// </summary>
public record SyncCheckResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
}

/// <summary>
/// Compares client-reported device files against the server's <see cref="SongDevice"/> state for
/// a device owned by the current user, producing <see cref="DeviceSyncSessionRecord"/> entries
/// describing the required sync actions (create/update/delete remote, update local, conflict,
/// skipped, ...). Extracted from <c>DevicesController.CheckSync</c> (the ~280-line method) as part
/// of Phase 11 of the controllers refactor so the controller stays thin (input/output + DTO
/// mapping only). Reuses <see cref="IDeviceLookupService"/> and <see cref="ISyncSessionLookupService"/>
/// for identity checks, <see cref="ISyncPathResolver"/> for naming/path-collision logic,
/// <see cref="ISyncComparisonHelper"/> for timestamp comparisons, and
/// <see cref="ISyncActionsServerFactory"/> to persist the records that are not tentative.
/// </summary>
public interface ISyncCheckService
{
    /// <summary>
    /// Runs the sync check for <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Returns <c>null</c> when no such device or session
    /// exists for the owner (mirrors the previous controller <c>NotFound</c> path). Throws when
    /// the session is not in progress (mirrors the previous controller behavior).
    /// </summary>
    Task<SyncCheckResult?> CheckAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncCheckInput input,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="ISyncCheckService"/>.
/// </summary>
public class SyncCheckService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncPathResolver pathResolver,
    ISyncComparisonHelper comparisonHelper,
    IOptions<Config> config,
    ILogger<SyncCheckService> logger) : ISyncCheckService
{
    // Lazy-loaded cache of all device paths, used only by the CreateLocal fallback branch.
    // Reset at the start of each CheckAsync call; the service is scoped (one instance per
    // request) so this is safe.
    private HashSet<string>? _usedPaths;

    /// <inheritdoc />
    public async Task<SyncCheckResult?> CheckAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncCheckInput input,
        CancellationToken cancellationToken)
    {
        _usedPaths = null;

        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var activeSessionResult = await sessionLookup.GetActiveSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (!activeSessionResult.Found)
        {
            if (activeSessionResult.Failure == ActiveSessionFailure.NotFound) return null;
            throw new Exception($"Sync session {activeSessionResult.NotInProgressSessionId} is not in progress (status: {activeSessionResult.NotInProgressStatus})");
        }

        var activeSession = activeSessionResult.Session!;
        var syncActions = syncActionsServerFactory.Create(db, activeSession.Id, deviceId, activeSession.IsDryRun);

        var namingStrategy = new TemplateNamingStrategy(
            device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

        var clientPaths = input.Files.Select(f => f.Path).ToList();

        // Load only the SongDevices for paths the client reported in this chunk, indexed by
        // DevicePath for O(1) lookup inside the per-file loop.
        var existingSongDevicesByPath = await db.SongDevices
            .IncludeSongMetadata("Song")
            .Where(sd => sd.DeviceId == deviceId && clientPaths.Contains(sd.DevicePath))
            .ToDictionaryAsync(sd => sd.DevicePath, cancellationToken);

        var allRecords = new List<DeviceSyncSessionRecord>();

        foreach (var clientFile in input.Files)
        {
            var existingSongDevice = existingSongDevicesByPath.GetValueOrDefault(clientFile.Path);

            logger.LogDebug("CheckSync: Path='{Path}', DeviceId={DeviceId}, SongDeviceFound={Found}, SongId={SongId}, LastSyncedModifiedAt={LastSynced}",
                clientFile.Path, deviceId, existingSongDevice != null, existingSongDevice?.SongId, existingSongDevice?.LastSyncedModifiedAt);

            if (existingSongDevice == null)
            {
                logger.LogDebug("CheckSync: Path='{Path}' -> CREATE_REMOTE (no existing SongDevice)", clientFile.Path);
                allRecords.Add(NewCreateRemoteRecord(activeSession.Id, clientFile));
            }
            // When a Song is deleted, the deletion services always null SongId and set SyncAction = Remove,
            // so the Song == null case is covered by the Remove branch below. The `|| Song == null` is kept
            // defensively in case that invariant is ever broken - we still want to delete the file on the device.
            else if (existingSongDevice.SyncAction == SongSyncAction.Remove || existingSongDevice.Song == null)
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> DELETE_LOCAL (marked for removal or song deleted)", clientFile.Path, existingSongDevice.SongId);
                var record = await syncActions.ActionDeleteLocal(existingSongDevice.DevicePath, existingSongDevice.SongId, "Song marked for removal or deleted on server", cancellationToken);
                allRecords.Add(record);
            }
            else if (input.Force)
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (Force flag)", clientFile.Path, existingSongDevice.SongId);
                allRecords.Add(NewUpdateRemoteRecord(activeSession.Id, clientFile, existingSongDevice.SongId, "Force flag was set"));
            }
            else if (existingSongDevice.LastSyncedModifiedAt == null)
            {
                await ProcessNoSyncTimestampAsync(activeSession.Id, clientFile, existingSongDevice, syncActions, allRecords, cancellationToken);
            }
            // Check if the device file was changed after the last sync.
            else if (comparisonHelper.IsNewerThan(clientFile.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
            {
                ProcessClientNewer(activeSession.Id, clientFile, existingSongDevice, allRecords);
            }
            // Check if the server song was changed after the last sync (device file unchanged).
            else
            {
                await ProcessServerMaybeNewerAsync(activeSession.Id, deviceId, clientFile, existingSongDevice, syncActions, namingStrategy, allRecords, cancellationToken);
            }
        }

        logger.LogInformation(
            "Sync check for device {DeviceId}: {TotalRecords} total records ({CreateRemote} create remote, {UpdateRemote} update remote, {Conflict} conflicts, {UpdateLocal} update local, {Skipped} skipped, {Link} link, {Unlink} unlink)",
            deviceId, allRecords.Count,
            allRecords.Count(r => r.Action == SyncRecordAction.CreateRemote),
            allRecords.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            allRecords.Count(r => r.Action == SyncRecordAction.Conflict),
            allRecords.Count(r => r.Action == SyncRecordAction.UpdateLocal),
            allRecords.Count(r => r.Action == SyncRecordAction.Skipped),
            allRecords.Count(r => r.Action == SyncRecordAction.Link),
            allRecords.Count(r => r.Action == SyncRecordAction.Unlink));

        return new SyncCheckResult { Records = allRecords };
    }

    /// <summary>
    /// Handles the <c>LastSyncedModifiedAt == null</c> branch: the device file exists on the
    /// server but has never been synced. When the device is in <see cref="SongSyncAction.Download"/>
    /// state and the server changed since the device was added, a conflict is recorded; otherwise
    /// an <c>UpdateRemote</c> is produced so the client uploads its copy.
    /// </summary>
    private async Task ProcessNoSyncTimestampAsync(
        long sessionId,
        SyncCheckFileInfo clientFile,
        SongDevice existingSongDevice,
        ISyncActionsServer syncActions,
        List<DeviceSyncSessionRecord> allRecords,
        CancellationToken cancellationToken)
    {
        // Sync comparisons use FileModifiedAt (the file-content change time), falling back to
        // ModifiedAt when FileModifiedAt is null (e.g. rows not yet backfilled). This ensures
        // metadata-only edits (which bump ModifiedAt but not FileModifiedAt) do not trigger
        // unnecessary device updates.
        var songFileModifiedAt = existingSongDevice.Song!.FileModifiedAt ?? existingSongDevice.Song.ModifiedAt;

        if (existingSongDevice.SyncAction == SongSyncAction.Download)
        {
            var referenceTime = existingSongDevice.AddedAt;
            if (comparisonHelper.IsNewerThan(songFileModifiedAt, referenceTime))
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> CONFLICT (Download action, server modified since added)", clientFile.Path, existingSongDevice.SongId);
                var record = await syncActions.ActionConflict(clientFile.Path, clientFile.ModifiedAt.ToUniversalTime(), songFileModifiedAt.ToUniversalTime(), existingSongDevice.SongId, reason: "Conflict: server modified since device added and no sync timestamp", cancellationToken: cancellationToken);
                record.Data = SyncActionDataSerializer.Serialize(new SyncCheckConflictData
                {
                    LocalModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                    ServerModifiedAt = songFileModifiedAt.ToUniversalTime(),
                    LastSyncedAt = existingSongDevice.LastSyncedModifiedAt?.ToUniversalTime(),
                    ServerChecksum = existingSongDevice.Song.Checksum,
                    ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                });
                db.Entry(record).Property(r => r.Data).IsModified = true;
                await db.SaveChangesAsync(cancellationToken);
                allRecords.Add(record);
            }
            else
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (no sync timestamp, Download action, server not newer)", clientFile.Path, existingSongDevice.SongId);
                allRecords.Add(NewUpdateRemoteRecord(sessionId, clientFile, existingSongDevice.SongId,
                    $"Local file exists but never synced, server has not changed since device was added (server modified at {songFileModifiedAt:O})"));
            }
        }
        else
        {
            logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (no sync timestamp)", clientFile.Path, existingSongDevice.SongId);
            allRecords.Add(NewUpdateRemoteRecord(sessionId, clientFile, existingSongDevice.SongId,
                $"Local file exists with no sync timestamp (local modified at {clientFile.ModifiedAt:O})"));
        }
    }

    /// <summary>
    /// Handles the "client file newer than last sync" branch. When the server song was also
    /// changed since the last sync, a conflict is recorded; otherwise an <c>UpdateRemote</c> is
    /// produced so the client uploads its newer copy.
    /// </summary>
    private void ProcessClientNewer(
        long sessionId,
        SyncCheckFileInfo clientFile,
        SongDevice existingSongDevice,
        List<DeviceSyncSessionRecord> allRecords)
    {
        var songFileModifiedAt = existingSongDevice.Song!.FileModifiedAt ?? existingSongDevice.Song.ModifiedAt;

        // Check if the server song was also changed after the last sync.
        if (comparisonHelper.IsNewerThan(songFileModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
        {
            logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> CONFLICT (local modified {LocalModifiedAt:O}, server modified {ServerModifiedAt:O}, last synced {LastSynced:O})",
                clientFile.Path, existingSongDevice.SongId, clientFile.ModifiedAt, songFileModifiedAt, existingSongDevice.LastSyncedModifiedAt);
            allRecords.Add(new DeviceSyncSessionRecord
            {
                SessionId = sessionId,
                FilePath = clientFile.Path,
                Action = SyncRecordAction.Conflict,
                SongId = existingSongDevice.SongId,
                Reason = "Conflict: both local and server modified since last sync",
                Data = SyncActionDataSerializer.Serialize(new SyncCheckConflictData
                {
                    LocalModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                    ServerModifiedAt = songFileModifiedAt.ToUniversalTime(),
                    LastSyncedAt = existingSongDevice.LastSyncedModifiedAt?.ToUniversalTime(),
                    ServerChecksum = existingSongDevice.Song.Checksum,
                    ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                }),
                ProcessedAt = DateTime.UtcNow,
            });
        }
        else
        {
            logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (file modified at {LocalModifiedAt:O} newer than last synced {LastSynced:O})",
                clientFile.Path, existingSongDevice.SongId, clientFile.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);
            allRecords.Add(NewUpdateRemoteRecord(sessionId, clientFile, existingSongDevice.SongId,
                $"File modified at {clientFile.ModifiedAt:O} is newer than last synced modified at {existingSongDevice.LastSyncedModifiedAt:O}"));
        }
    }

    /// <summary>
    /// Handles the "client file unchanged" branch. When the server song changed since the last
    /// sync, an <c>UpdateLocal</c>/<c>DeleteLocal</c>/<c>CreateLocal</c> is produced depending on
    /// the device's current state; otherwise the file is skipped.
    /// </summary>
    private async Task ProcessServerMaybeNewerAsync(
        long sessionId,
        long deviceId,
        SyncCheckFileInfo clientFile,
        SongDevice existingSongDevice,
        ISyncActionsServer syncActions,
        TemplateNamingStrategy namingStrategy,
        List<DeviceSyncSessionRecord> allRecords,
        CancellationToken cancellationToken)
    {
        var songFileModifiedAt = existingSongDevice.Song!.FileModifiedAt ?? existingSongDevice.Song.ModifiedAt;

        if (comparisonHelper.IsNewerThan(songFileModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
        {
            logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_LOCAL (server modified {ServerModifiedAt:O}, last synced {LastSynced:O})",
                clientFile.Path, existingSongDevice.SongId, songFileModifiedAt, existingSongDevice.LastSyncedModifiedAt);

            if (existingSongDevice.SyncAction == SongSyncAction.Remove)
            {
                var record = await syncActions.ActionDeleteLocal(existingSongDevice.DevicePath, existingSongDevice.SongId, "Song marked for removal", cancellationToken);
                allRecords.Add(record);
            }
            else if (existingSongDevice.LastSyncedModifiedAt != null)
            {
                allRecords.Add(new DeviceSyncSessionRecord
                {
                    SessionId = sessionId,
                    FilePath = clientFile.Path,
                    Action = SyncRecordAction.UpdateLocal,
                    SongId = existingSongDevice.SongId!.Value,
                    Reason = $"Server modified since last sync (server modified at {songFileModifiedAt:O}, last synced at {existingSongDevice.LastSyncedModifiedAt:O})",
                    Data = SyncActionDataSerializer.Serialize(new SyncCheckUpdateLocalData
                    {
                        LocalModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                        ServerModifiedAt = songFileModifiedAt.ToUniversalTime(),
                        LastSyncedAt = existingSongDevice.LastSyncedModifiedAt!.Value.ToUniversalTime(),
                        ServerChecksum = existingSongDevice.Song.Checksum,
                        ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                    }),
                    ProcessedAt = DateTime.UtcNow,
                });
            }
            else
            {
                // Lazy-load the full set of device paths, which are only needed for naming-collision
                // detection on CreateLocal. Cached across iterations in _usedPaths.
                _usedPaths ??= await db.SongDevices
                    .Where(sd => sd.DeviceId == deviceId)
                    .Select(sd => sd.DevicePath)
                    .ToHashSetAsync(cancellationToken);

                var pendingAction = pathResolver.ComputePendingActionPath(existingSongDevice, namingStrategy, _usedPaths);
                _usedPaths.Add(pendingAction.Path);

                var reason = $"Server modified at {songFileModifiedAt:O} is newer than last synced at {existingSongDevice.LastSyncedModifiedAt:O}";
                var record = await syncActions.ActionCreateLocal(pendingAction.Path, existingSongDevice.SongId, songFileModifiedAt, reason, cancellationToken);
                allRecords.Add(record);
            }
        }
        else
        {
            logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> SKIPPED (unchanged)", clientFile.Path, existingSongDevice.SongId);
            var record = await syncActions.ActionSkipped(clientFile.Path, existingSongDevice.SongId, reason: "File unchanged since last sync", cancellationToken: cancellationToken);
            allRecords.Add(record);
        }
    }

    /// <summary>
    /// Builds a tentative (non-persisted) <see cref="SyncRecordAction.CreateRemote"/> record for a
    /// client file with no matching <see cref="SongDevice"/> on the server.
    /// </summary>
    private static DeviceSyncSessionRecord NewCreateRemoteRecord(long sessionId, SyncCheckFileInfo clientFile)
    {
        return new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = clientFile.Path,
            Action = SyncRecordAction.CreateRemote,
            SongId = null,
            Reason = $"No matching SongDevice found on server for path '{clientFile.Path}'",
            Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
            {
                ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                Reason = $"No matching SongDevice found on server for path '{clientFile.Path}'",
            }),
            ProcessedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Builds a tentative (non-persisted) <see cref="SyncRecordAction.UpdateRemote"/> record.
    /// </summary>
    private static DeviceSyncSessionRecord NewUpdateRemoteRecord(long sessionId, SyncCheckFileInfo clientFile, long? songId, string reason)
    {
        return new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = clientFile.Path,
            Action = SyncRecordAction.UpdateRemote,
            SongId = songId,
            Reason = reason,
            Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
            {
                ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                Reason = reason,
            }),
            ProcessedAt = DateTime.UtcNow,
        };
    }
}
