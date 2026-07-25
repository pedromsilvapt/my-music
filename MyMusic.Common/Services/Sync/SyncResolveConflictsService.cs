using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// A single client-reported conflict descriptor for a sync resolve-conflicts request. Mirrors the
/// server-side fields of <c>SyncConflictResolveItem</c> but lives in <see cref="MyMusic.Common"/>
/// so the service has no dependency on the Server DTO layer.
/// </summary>
public record SyncResolveConflictItem
{
    public required string Path { get; init; }

    public required long SongId { get; init; }

    public required string FileContentBase64 { get; init; }

    public required DateTime LocalModifiedAt { get; init; }
}

/// <summary>
/// A single client-reported potential-update descriptor for a sync resolve-conflicts request.
/// Mirrors <c>SyncPotentialUpdateResolveItem</c> without the DTO dependency.
/// </summary>
public record SyncResolvePotentialUpdateItem
{
    public required string Path { get; init; }

    public required long SongId { get; init; }

    public required string FileContentBase64 { get; init; }

    public required DateTime LocalModifiedAt { get; init; }

    public required DateTime LastSyncedAt { get; init; }
}

/// <summary>
/// Input for a sync resolve-conflicts operation. Mirrors <c>SyncResolveConflictsRequest</c>
/// without the DTO dependency.
/// </summary>
public record SyncResolveConflictsInput
{
    public required List<SyncResolveConflictItem> Conflicts { get; init; }

    public required List<SyncResolvePotentialUpdateItem> PotentialUpdates { get; init; }
}

/// <summary>
/// Result of a sync resolve-conflicts operation. The controller maps this to
/// <c>SyncResolveConflictsResponse</c>.
/// </summary>
public record SyncResolveConflictsResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
}

/// <summary>
/// Resolves client-reported sync conflicts and potential updates for a device sync session owned
/// by the current user. For each conflict/potential-update, the local file content (base64) is
/// compared against the server song checksum: matching checksums produce a timestamp-update record
/// (no file transfer), while differing checksums produce a conflict record (conflicts) or an
/// update-local record (potential updates, optionally followed by a rename record when the naming
/// template changed the target path). Extracted from <c>DevicesController.ResolveConflicts</c>
/// (the ~190-line method) as part of Phase 12 of the controllers refactor so the controller stays
/// thin (input/output + DTO mapping only). Reuses <see cref="IDeviceLookupService"/> and
/// <see cref="ISyncSessionLookupService"/> for identity checks, <see cref="ISyncPathResolver"/>
/// for naming/path-collision logic, and <see cref="ISyncActionsServerFactory"/> to persist the
/// records.
/// </summary>
public interface ISyncResolveConflictsService
{
    /// <summary>
    /// Resolves the conflicts/potential-updates for <paramref name="sessionId"/> scoped to
    /// <paramref name="deviceId"/> owned by <paramref name="ownerId"/>. Returns <c>null</c> when
    /// no such device or session exists for the owner (mirrors the previous controller
    /// <c>NotFound</c> path). Throws when the session is not in progress (mirrors the previous
    /// controller behavior).
    /// </summary>
    Task<SyncResolveConflictsResult?> ResolveAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncResolveConflictsInput input,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="ISyncResolveConflictsService"/>.
/// </summary>
public class SyncResolveConflictsService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncPathResolver pathResolver,
    IOptions<Config> config,
    ILogger<SyncResolveConflictsService> logger) : ISyncResolveConflictsService
{
    /// <inheritdoc />
    public async Task<SyncResolveConflictsResult?> ResolveAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncResolveConflictsInput input,
        CancellationToken cancellationToken)
    {
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

        var records = new List<DeviceSyncSessionRecord>();

        foreach (var conflict in input.Conflicts)
        {
            await ProcessConflictAsync(deviceId, conflict, syncActions, records, cancellationToken);
        }

        // Process potential updates: server was modified after last sync, client file was unchanged.
        // Compare checksums to determine if a local update is actually needed.
        if (input.PotentialUpdates.Count > 0)
        {
            var namingStrategy = new TemplateNamingStrategy(
                device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

            var usedPaths = new HashSet<string>(await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId)
                .Select(sd => sd.DevicePath)
                .ToHashSetAsync(cancellationToken));

            foreach (var update in input.PotentialUpdates)
            {
                await ProcessPotentialUpdateAsync(deviceId, update, namingStrategy, usedPaths, syncActions, records, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved conflicts for device {DeviceId}: {RecordCount} records",
            deviceId, records.Count);

        return new SyncResolveConflictsResult { Records = records };
    }

    /// <summary>
    /// Handles a single conflict item: looks up the <see cref="SongDevice"/>, decodes the base64
    /// file content, and compares the local checksum against the server song checksum. Matching
    /// checksums produce an <c>UpdateTimestamp</c> record; differing checksums produce a
    /// <c>Conflict</c> record. A missing <see cref="SongDevice"/> or invalid base64 produces an
    /// <c>Error</c> record (or is skipped when the SongDevice is not found).
    /// </summary>
    private async Task ProcessConflictAsync(
        long deviceId,
        SyncResolveConflictItem conflict,
        ISyncActionsServer syncActions,
        List<DeviceSyncSessionRecord> records,
        CancellationToken cancellationToken)
    {
        var songDevice = await db.SongDevices
            .Include(sd => sd.Song)
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == conflict.SongId, cancellationToken);

        if (songDevice == null)
        {
            logger.LogWarning("SongDevice not found for device {DeviceId} and song {SongId}", deviceId, conflict.SongId);
            return;
        }

        var (fileBytes, errorRecord) = await DecodeFileBytesAsync(conflict.FileContentBase64, conflict.Path, conflict.SongId, syncActions, cancellationToken);
        if (errorRecord != null)
        {
            records.Add(errorRecord);
            return;
        }

        var localChecksum = ChecksumService.ComputeChecksumFromBytes(fileBytes!, songDevice.Song.ChecksumAlgorithm);

        if (localChecksum == songDevice.Song.Checksum)
        {
            var localModifiedAtUtc = conflict.LocalModifiedAt.ToUniversalTime();

            // Use FileModifiedAt (the file-content change time) rather than ModifiedAt (which
            // also bumps on metadata-only edits). This prevents a metadata-only edit from
            // inflating the device's new LastSyncedModifiedAt, which would mask real future
            // file-content changes.
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;

            var newLastSynced = localModifiedAtUtc > songFileModifiedAt
                ? localModifiedAtUtc
                : songFileModifiedAt;

            var tsRecord = await syncActions.ActionUpdateTimestamp(conflict.Path, newLastSynced, conflict.SongId, "Timestamp update: checksums match, no file change needed", modifiedAt: conflict.LocalModifiedAt, createdAt: songDevice.AddedAt, cancellationToken: cancellationToken);

            logger.LogInformation(
                "Resolved conflict for {Path} - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                conflict.Path, newLastSynced);

            records.Add(tsRecord);
            return;
        }
        else
        {
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;
            var conflictRecord = await syncActions.ActionConflict(conflict.Path, conflict.LocalModifiedAt, songFileModifiedAt, conflict.SongId, "Conflict: local and server both modified, checksums differ", localChecksum: localChecksum, serverChecksum: songDevice.Song.Checksum, algorithm: songDevice.Song.ChecksumAlgorithm, cancellationToken);

            logger.LogError(
                "Conflict detected for {Path} - checksums differ (local: {LocalChecksum}, server: {ServerChecksum}), marking as error",
                conflict.Path, localChecksum, songDevice.Song.Checksum);

            records.Add(conflictRecord);
        }
    }

    /// <summary>
    /// Handles a single potential-update item: looks up the <see cref="SongDevice"/> (with full
    /// song metadata), decodes the base64 file content, and compares the local checksum against
    /// the server song checksum. Matching checksums produce an <c>UpdateTimestamp</c> record;
    /// differing checksums produce an <c>UpdateLocal</c> record (optionally followed by a
    /// <c>Rename</c> record when the naming template changed the target path). Missing
    /// SongDevice/Song or invalid base64 are skipped or produce an <c>Error</c> record.
    /// </summary>
    private async Task ProcessPotentialUpdateAsync(
        long deviceId,
        SyncResolvePotentialUpdateItem update,
        TemplateNamingStrategy namingStrategy,
        HashSet<string> usedPaths,
        ISyncActionsServer syncActions,
        List<DeviceSyncSessionRecord> records,
        CancellationToken cancellationToken)
    {
        var songDevice = await db.SongDevices
            .IncludeSongMetadata("Song")
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == update.SongId, cancellationToken);

        if (songDevice == null)
        {
            logger.LogWarning("SongDevice not found for device {DeviceId} and song {SongId} during potential update resolution", deviceId, update.SongId);
            return;
        }

        if (songDevice.Song == null)
        {
            logger.LogWarning("Song not found for SongDevice device {DeviceId} and song {SongId} during potential update resolution", deviceId, update.SongId);
            return;
        }

        var (fileBytes, errorRecord) = await DecodeFileBytesAsync(update.FileContentBase64, update.Path, update.SongId, syncActions, cancellationToken);
        if (errorRecord != null)
        {
            records.Add(errorRecord);
            return;
        }

        var localChecksum = ChecksumService.ComputeChecksumFromBytes(fileBytes!, songDevice.Song.ChecksumAlgorithm);

        if (localChecksum == songDevice.Song.Checksum)
        {
            // Use FileModifiedAt (file-content change time) for the new LastSynced
            // computation so metadata-only edits do not inflate the sync timestamp.
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;

            var newLastSynced = update.LocalModifiedAt.ToUniversalTime() > songFileModifiedAt
                ? update.LocalModifiedAt.ToUniversalTime()
                : songFileModifiedAt;

            var tsRecord = await syncActions.ActionUpdateTimestamp(update.Path, newLastSynced, update.SongId, "Timestamp update: server was modified but checksums match, no local update needed", modifiedAt: update.LocalModifiedAt, createdAt: songDevice.AddedAt, cancellationToken: cancellationToken);

            records.Add(tsRecord);

            logger.LogInformation(
                "Resolved potential update for {Path} (SongId={SongId}) - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                update.Path, update.SongId, newLastSynced);
        }
        else
        {
            var pendingAction = pathResolver.ComputePendingActionPath(songDevice, namingStrategy, usedPaths);
            usedPaths.Add(pendingAction.Path);

            var updateFilePath = pendingAction.PreviousPath ?? pendingAction.Path;
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;
            var reason = $"Server modified at {songFileModifiedAt:O} is newer than last synced at {update.LastSyncedAt:O}, checksums differ";
            var updateRecord = await syncActions.ActionUpdateLocal(updateFilePath, update.SongId, songFileModifiedAt, reason, cancellationToken);
            records.Add(updateRecord);

            if (pendingAction.PreviousPath != null)
            {
                var renameRecord = await syncActions.ActionRename(pendingAction.Path, pendingAction.PreviousPath, pendingAction.Path, update.SongId, "Path updated by naming template", cancellationToken);
                records.Add(renameRecord);
            }

            logger.LogInformation(
                "Potential update for {Path} (SongId={SongId}) - checksums differ, creating UpdateLocal action",
                update.Path, update.SongId);
        }
    }

    /// <summary>
    /// Decodes a base64-encoded file content payload. On failure, produces an <c>Error</c> record
    /// via <paramref name="syncActions"/> describing the invalid content and returns it as the
    /// second tuple element; <c>fileBytes</c> is <c>null</c> in that case.
    /// </summary>
    private async Task<(byte[]? FileBytes, DeviceSyncSessionRecord? ErrorRecord)> DecodeFileBytesAsync(
        string fileContentBase64,
        string path,
        long songId,
        ISyncActionsServer syncActions,
        CancellationToken cancellationToken)
    {
        try
        {
            return (Convert.FromBase64String(fileContentBase64), null);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Invalid base64 content for {Path}", path);

            var errorRecord = await syncActions.ActionError(path, "Invalid file content format", songId, "Invalid file content format", cancellationToken);
            return (null, errorRecord);
        }
    }
}
