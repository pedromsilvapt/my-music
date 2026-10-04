using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncConflictChooseService"/>.
/// </summary>
public class SyncConflictChooseService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncPathResolver pathResolver,
    IOptions<Config> config,
    ILogger<SyncConflictChooseService> logger) : ISyncConflictChooseService
{
    /// <inheritdoc />
    public async Task<SyncConflictChooseResult?> ChooseDownloadAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        IReadOnlyCollection<long> conflictRecordIds,
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
        var records = new List<DeviceSyncSessionRecord>();

        if (conflictRecordIds.Count == 0)
        {
            return new SyncConflictChooseResult { Records = records };
        }

        var conflicts = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == activeSession.Id
                        && r.Action == SyncRecordAction.Conflict
                        && conflictRecordIds.Contains(r.Id)
                        && !db.DeviceSyncSessionRecords.Any(o => o.ResolvesConflictRecordId == r.Id))
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var syncActions = syncActionsServerFactory.Create(db, activeSession.Id, deviceId, activeSession.IsDryRun);

        var namingStrategy = new TemplateNamingStrategy(
            activeSession.NamingTemplate ?? device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

        var usedPaths = new HashSet<string>(await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => sd.DevicePath)
            .ToHashSetAsync(cancellationToken));

        foreach (var conflict in conflicts)
        {
            var songDevice = await db.SongDevices
                .IncludeSongMetadata("Song")
                .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == conflict.SongId && sd.DevicePath == conflict.FilePath, cancellationToken);

            if (songDevice?.Song == null)
            {
                logger.LogWarning("SongDevice not found for device {DeviceId}, song {SongId} and path {Path} of conflict {ConflictRecordId}", deviceId, conflict.SongId, conflict.FilePath, conflict.Id);
                continue;
            }

            if (activeSession.Direction == SyncDirection.Up)
            {
                records.Add(await syncActions.ActionSkipped(conflict.FilePath, conflict.SongId, "Conflict: server version chosen, not downloaded (direction up)", cancellationToken));
                continue;
            }

            var resolving = await SyncUpdateLocalRecords.AddAsync(
                pathResolver, songDevice, songDevice.Song.Id, "Conflict resolved by user: server version wins",
                namingStrategy, usedPaths, syncActions, cancellationToken);

            foreach (var record in resolving)
            {
                record.ResolvesConflictRecordId = conflict.Id;
            }

            records.AddRange(resolving);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved {ConflictCount} conflicts with the server version for device {DeviceId}: {RecordCount} records",
            conflicts.Count, deviceId, records.Count);

        return new SyncConflictChooseResult { Records = records };
    }
}
