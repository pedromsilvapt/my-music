using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Songs;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncResolveConflictsService"/>.
/// </summary>
public class SyncResolveConflictsService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncPathResolver pathResolver,
    ISyncUsedPathsService usedPathsService,
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

        if (input.Conflicts.Count == 0 && input.PotentialUpdates.Count == 0)
        {
            return new SyncResolveConflictsResult { Records = records };
        }

        // One transaction for the whole request: a failure midway leaves no records behind, which the
        // device would never receive, and so never acknowledge
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Both conflicts (stale local copies) and potential updates can produce UpdateLocal actions,
        // whose target paths are computed with the device's naming template
        var namingStrategy = new TemplateNamingStrategy(
            activeSession.NamingTemplate ?? device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

        var usedPaths = await usedPathsService.GetAsync(db, deviceId, activeSession.Id, cancellationToken);

        foreach (var conflict in input.Conflicts)
        {
            await ProcessConflictAsync(deviceId, conflict, activeSession.Direction, namingStrategy, usedPaths, syncActions, records, cancellationToken);
        }

        // Process potential updates: server was modified after last sync, client file was unchanged.
        // Compare checksums to determine if a local update is actually needed.
        foreach (var update in input.PotentialUpdates)
        {
            await ProcessPotentialUpdateAsync(deviceId, update, activeSession.Direction, namingStrategy, usedPaths, syncActions, records, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Resolved conflicts for device {DeviceId}: {RecordCount} records",
            deviceId, records.Count);

        return new SyncResolveConflictsResult { Records = records };
    }

    /// <summary>
    /// Handles a single conflict item: looks up the <see cref="SongDevice"/> at the item's path, resolves
    /// the local checksum, and compares it against the server song checksum. Matching
    /// checksums produce an <c>UpdateTimestamp</c> record. A local checksum matching an older version
    /// of the song means the local file is a stale copy, so the server wins: an <c>UpdateLocal</c>
    /// record (<c>Skipped</c> in <c>up</c> direction). Any other difference produces a
    /// <c>Conflict</c> record. A local checksum that cannot be resolved produces an <c>Error</c>
    /// record; the item is skipped when the SongDevice is not found.
    /// </summary>
    private async Task ProcessConflictAsync(
        long deviceId,
        SyncResolveConflictItem conflict,
        SyncDirection direction,
        TemplateNamingStrategy namingStrategy,
        SyncUsedPaths usedPaths,
        ISyncActionsServer syncActions,
        List<DeviceSyncSessionRecord> records,
        CancellationToken cancellationToken)
    {
        var songDevice = await db.SongDevices
            .IncludeSongMetadata("Song")
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == conflict.SongId && sd.DevicePath == conflict.Path, cancellationToken);

        if (songDevice == null)
        {
            logger.LogWarning("SongDevice not found for device {DeviceId}, song {SongId} and path {Path}", deviceId, conflict.SongId, conflict.Path);
            return;
        }

        var (localChecksum, errorRecord) = await ResolveLocalChecksumAsync(conflict.Checksum, conflict.ChecksumAlgorithm, conflict.FileContentBase64, songDevice.Song, conflict.Path, conflict.SongId, syncActions, cancellationToken);
        if (errorRecord != null)
        {
            records.Add(errorRecord);
            return;
        }

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
        else if (await SongChecksumHistory.IsPreviousVersionAsync(db, songDevice.Song, localChecksum,
                     songDevice.Song.ChecksumAlgorithm, cancellationToken))
        {
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;

            if (direction == SyncDirection.Up)
            {
                var reason = $"Local file is a previous version of the song, server modified at {songFileModifiedAt:O}, not downloaded (direction up)";
                records.Add(await syncActions.ActionSkipped(conflict.Path, conflict.SongId, reason, cancellationToken));
            }
            else
            {
                var reason = $"Local file is a previous version of the song, server modified at {songFileModifiedAt:O} wins";
                records.AddRange(await SyncUpdateLocalRecords.AddAsync(pathResolver, songDevice, conflict.SongId, reason, namingStrategy, usedPaths, syncActions, cancellationToken));
            }

            logger.LogInformation(
                "Resolved conflict for {Path} - local checksum {LocalChecksum} is a previous version of song {SongId}, server wins",
                conflict.Path, localChecksum, conflict.SongId);
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
    /// Handles a single potential-update item: looks up the <see cref="SongDevice"/> at the item's path (with full
    /// song metadata), resolves the local checksum, and compares it against
    /// the server song checksum. Matching checksums produce an <c>UpdateTimestamp</c> record;
    /// differing checksums produce an <c>UpdateLocal</c> record (optionally followed by a
    /// <c>Rename</c> record when the naming template changed the target path). Missing
    /// SongDevice/Song are skipped; a local checksum that cannot be resolved produces an <c>Error</c> record.
    /// In <c>up</c> direction the device never processes server actions, so differing checksums
    /// produce a <c>Skipped</c> record instead of <c>UpdateLocal</c>/<c>Rename</c>.
    /// </summary>
    private async Task ProcessPotentialUpdateAsync(
        long deviceId,
        SyncResolvePotentialUpdateItem update,
        SyncDirection direction,
        TemplateNamingStrategy namingStrategy,
        SyncUsedPaths usedPaths,
        ISyncActionsServer syncActions,
        List<DeviceSyncSessionRecord> records,
        CancellationToken cancellationToken)
    {
        var songDevice = await db.SongDevices
            .IncludeSongMetadata("Song")
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == update.SongId && sd.DevicePath == update.Path, cancellationToken);

        if (songDevice == null)
        {
            logger.LogWarning("SongDevice not found for device {DeviceId}, song {SongId} and path {Path} during potential update resolution", deviceId, update.SongId, update.Path);
            return;
        }

        if (songDevice.Song == null)
        {
            logger.LogWarning("Song not found for SongDevice device {DeviceId} and song {SongId} during potential update resolution", deviceId, update.SongId);
            return;
        }

        var (localChecksum, errorRecord) = await ResolveLocalChecksumAsync(update.Checksum, update.ChecksumAlgorithm, update.FileContentBase64, songDevice.Song, update.Path, update.SongId, syncActions, cancellationToken);
        if (errorRecord != null)
        {
            records.Add(errorRecord);
            return;
        }

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
        else if (direction == SyncDirection.Up)
        {
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;
            var reason = $"Server modified at {songFileModifiedAt:O} is newer than last synced at {update.LastSyncedAt:O}, checksums differ, not downloaded (direction up)";
            var skippedRecord = await syncActions.ActionSkipped(update.Path, update.SongId, reason, cancellationToken);
            records.Add(skippedRecord);
        }
        else
        {
            var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;
            var reason = $"Server modified at {songFileModifiedAt:O} is newer than last synced at {update.LastSyncedAt:O}, checksums differ";
            records.AddRange(await SyncUpdateLocalRecords.AddAsync(pathResolver, songDevice, update.SongId, reason, namingStrategy, usedPaths, syncActions, cancellationToken));

            logger.LogInformation(
                "Potential update for {Path} (SongId={SongId}) - checksums differ, creating UpdateLocal action",
                update.Path, update.SongId);
        }
    }

    /// <summary>
    /// Resolves the checksum of the client's local file. A client-computed checksum is used as is
    /// when its algorithm matches the song's; legacy clients send the whole file as base64 instead,
    /// which is decoded and hashed here. An unsupported algorithm, invalid base64, or an item with
    /// neither field produces an <c>Error</c> record, returned as the second tuple element with a
    /// <c>null</c> checksum.
    /// </summary>
    private async Task<(string? LocalChecksum, DeviceSyncSessionRecord? ErrorRecord)> ResolveLocalChecksumAsync(
        string? checksum,
        string? checksumAlgorithm,
        string? fileContentBase64,
        Song song,
        string path,
        long songId,
        ISyncActionsServer syncActions,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(checksum))
        {
            if (checksumAlgorithm == song.ChecksumAlgorithm) return (checksum, null);

            logger.LogError(
                "Unsupported checksum algorithm {Algorithm} for {Path}, song {SongId} uses {SongAlgorithm}",
                checksumAlgorithm, path, songId, song.ChecksumAlgorithm);

            return (null, await syncActions.ActionError(path, "Unsupported checksum algorithm", songId, "Unsupported checksum algorithm", cancellationToken: cancellationToken));
        }

        if (fileContentBase64 == null)
        {
            logger.LogError("Neither a checksum nor the file content was sent for {Path}", path);

            return (null, await syncActions.ActionError(path, "Missing checksum or file content", songId, "Missing checksum or file content", cancellationToken: cancellationToken));
        }

        var (fileBytes, errorRecord) = await DecodeFileBytesAsync(fileContentBase64, path, songId, syncActions, cancellationToken);
        if (errorRecord != null) return (null, errorRecord);

        return (ChecksumService.ComputeChecksumFromBytes(fileBytes!, song.ChecksumAlgorithm), null);
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

            var errorRecord = await syncActions.ActionError(path, "Invalid file content format", songId, "Invalid file content format", cancellationToken: cancellationToken);
            return (null, errorRecord);
        }
    }
}