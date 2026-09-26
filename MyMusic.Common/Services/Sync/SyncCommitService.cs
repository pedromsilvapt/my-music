using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;

namespace MyMusic.Common.Services.Sync;

public class SyncCommitService(
    IFileSystem fileSystem,
    IMusicService musicService,
    ILoggerFactory loggerFactory,
    ILogger<SyncCommitService> logger) : ISyncCommitService
{
    public async Task<SyncCommitResult> CommitAsync(
        MusicDbContext db, long sessionId, long deviceId, bool isDryRun,
        CancellationToken cancellationToken = default)
    {
        var session = await db.DeviceSyncSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session?.Status == SyncSessionStatus.Committed)
        {
            return BuildExistingResult(session, await db.DeviceSyncSessionRecords
                .Where(r => r.SessionId == sessionId)
                .ToListAsync(cancellationToken));
        }

        var records = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        // Validated in dry-run too: clients acknowledge client-action records in both modes, so a
        // dry-run commit must fail under exactly the same conditions as a real one.
        var unacknowledgedClientActions = records
            .Where(r => !r.Acknowledged && r.Action.IsClientAction())
            .ToList();

        if (unacknowledgedClientActions.Count > 0)
        {
            var unacknowledgedSummary = string.Join(", ",
                unacknowledgedClientActions.GroupBy(r => r.Action)
                    .Select(g => $"{g.Key}: {g.Count()}"));
            throw new InvalidOperationException(
                $"Cannot commit session {sessionId}: {unacknowledgedClientActions.Count} unacknowledged client-action records ({unacknowledgedSummary}). " +
                "Client must acknowledge all pending actions before commit.");
        }

        AcknowledgeServerActionRecords(records);

        var device = await db.Devices.FirstAsync(d => d.Id == deviceId, cancellationToken);
        var userId = device.OwnerId;

        // Orphans are detected before any record is processed. Record processing mutates SongDevices,
        // and some steps (song imports) save those changes mid-commit, so a query made afterwards
        // would see a different database state than a dry run does (e.g. a just-renamed SongDevice
        // would look orphaned). Detecting up front keeps the record list identical in both modes.
        var direction = session?.Direction ?? SyncDirection.Both;
        var orphanDetection = await DetectOrphansAsync(db, deviceId, records, direction, cancellationToken);

        var imports = new CommitImports();

        // Records whose action failed were not performed, so their bookkeeping is not applied either
        var failedRecordIds = records
            .Where(r => r.Action == SyncRecordAction.Error)
            .Select(r => SyncActionDataSerializer.Deserialize<ErrorData>(r.Data)?.FailedRecordId)
            .OfType<long>()
            .ToHashSet();

        foreach (var record in records)
        {
            if (failedRecordIds.Contains(record.Id))
                continue;

            await ProcessRecordAsync(db, sessionId, deviceId, record, isDryRun, userId, imports, cancellationToken);
        }

        HandleOrphans(db, sessionId, orphanDetection, isDryRun);

        await db.SaveChangesAsync(cancellationToken);

        var committedAt = DateTime.UtcNow;

        return BuildResult(await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(cancellationToken), committedAt);
    }

    private async Task ProcessRecordAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        long userId, CommitImports imports, CancellationToken cancellationToken)
    {
        switch (record.Action)
        {
            case SyncRecordAction.CreateRemote:
                await ProcessCreateRemoteAsync(db, sessionId, deviceId, record, isDryRun, userId, imports, cancellationToken);
                break;
            case SyncRecordAction.UpdateRemote:
                await ProcessUpdateRemoteAsync(db, sessionId, deviceId, record, isDryRun, userId, imports, cancellationToken);
                break;
            case SyncRecordAction.CreateLocal:
                await ProcessCreateLocalAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.UpdateLocal:
                await ProcessUpdateLocalAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.DeleteLocal:
                await ProcessDeleteLocalAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Link:
                await ProcessLinkAsync(db, sessionId, deviceId, record, isDryRun, imports, cancellationToken);
                break;
            case SyncRecordAction.Unlink:
                await ProcessUnlinkAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Rename:
                await ProcessRenameAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Skipped:
                await ProcessSkippedAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Conflict:
                break;
            case SyncRecordAction.UpdateTimestamp:
                await ProcessUpdateTimestampAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Error:
                break;
        }
    }

    private async Task ProcessCreateRemoteAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        long userId, CommitImports imports, CancellationToken cancellationToken)
    {
        var data = SyncActionDataSerializer.Deserialize<CreateRemoteData>(record.Data);
        var tempFilePath = data?.TempFilePath;
        var modifiedAt = data?.ModifiedAt;
        var createdAt = data?.CreatedAt;
        var checksum = data?.Checksum;
        var originalFilePath = data?.OriginalFilePath;

        if (isDryRun)
            return;

        var songId = data?.SongId ?? record.SongId;
        SongDevice? songDevice = null;

        if (tempFilePath != null)
        {
            if (!fileSystem.File.Exists(tempFilePath))
            {
                await RecordError(db, sessionId, record, $"Staged file not found: {tempFilePath}", cancellationToken);
                return;
            }

            if (songId.HasValue && songId.Value > 0)
            {
                var outcome = await ImportSongFromFile(db, tempFilePath, songId.Value, userId, originalFilePath: originalFilePath, cancellationToken: cancellationToken);
                if (outcome.ErrorMessage != null)
                {
                    imports.MarkFailed(checksum);
                    await RecordError(db, sessionId, record, outcome.ErrorMessage, cancellationToken);
                    return;
                }

                songDevice = await musicService.AddSongsToDevice(db, deviceId, songId.Value, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else if (checksum != null && imports.CreatedSongIds.TryGetValue(checksum, out var existingSongId))
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, existingSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else
            {
                var outcome = await ImportSongFromFile(db, tempFilePath, null, userId, createdAt, modifiedAt, originalFilePath, cancellationToken);
                if (outcome.ErrorMessage != null)
                {
                    imports.MarkFailed(checksum);
                    await RecordError(db, sessionId, record, outcome.ErrorMessage, cancellationToken);
                    return;
                }

                var newSongId = outcome.SongId!.Value;
                if (checksum != null)
                {
                    imports.CreatedSongIds[checksum] = newSongId;
                }

                songDevice = await musicService.AddSongsToDevice(db, deviceId, newSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
        }
        else
        {
            if (songId.HasValue && songId.Value > 0)
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, songId.Value, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else if (checksum != null && imports.CreatedSongIds.TryGetValue(checksum, out var existingSongId))
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, existingSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
        }

        var songAfterCreate = songId.HasValue && songId.Value > 0
            ? await db.Songs.FindAsync([songId.Value], cancellationToken)
            : null;
        logger.LogInformation("ProcessCreateRemoteAsync: path={Path}, songId={SongId}, song.FileModifiedAtTicks={SongFileModifiedAtTicks}, sdIsNull={SdIsNull}, lastSyncedIsNull={LastSyncedIsNull}",
            record.FilePath, songId, songAfterCreate?.FileModifiedAt?.Ticks, songDevice == null, songDevice?.LastSyncedModifiedAt == null);
    }

    private async Task ProcessUpdateRemoteAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        long userId, CommitImports imports, CancellationToken cancellationToken)
    {
        var data = SyncActionDataSerializer.Deserialize<UpdateRemoteData>(record.Data);
        var tempFilePath = data?.TempFilePath;
        var modifiedAt = data?.ModifiedAt;
        var createdAt = data?.CreatedAt;
        var checksum = data?.Checksum;
        var originalFilePath = data?.OriginalFilePath;

        if (isDryRun)
            return;

        var songId = data?.SongId ?? record.SongId;
        SongDevice? songDevice = null;

        if (tempFilePath != null)
        {
            if (!fileSystem.File.Exists(tempFilePath))
            {
                await RecordError(db, sessionId, record, $"Staged file not found: {tempFilePath}", cancellationToken);
                return;
            }

            if (songId.HasValue && songId.Value > 0)
            {
                // A failed import leaves the device's change unsynced, so the next sync retries it
                var outcome = await ImportSongFromFile(db, tempFilePath, songId.Value, userId, originalFilePath: originalFilePath, cancellationToken: cancellationToken);
                if (outcome.ErrorMessage != null)
                {
                    imports.MarkFailed(checksum);
                    await RecordError(db, sessionId, record, outcome.ErrorMessage, cancellationToken);
                    return;
                }
            }
            else if (checksum != null && imports.CreatedSongIds.TryGetValue(checksum, out var existingSongId))
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, existingSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else
            {
                var outcome = await ImportSongFromFile(db, tempFilePath, null, userId, createdAt, modifiedAt, originalFilePath, cancellationToken);
                if (outcome.ErrorMessage != null)
                {
                    imports.MarkFailed(checksum);
                    await RecordError(db, sessionId, record, outcome.ErrorMessage, cancellationToken);
                    return;
                }

                var newSongId = outcome.SongId!.Value;
                if (checksum != null)
                {
                    imports.CreatedSongIds[checksum] = newSongId;
                }

                songDevice = await musicService.AddSongsToDevice(db, deviceId, newSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
        }

        songDevice ??= await FindSongDeviceByIds(db, deviceId, record, data?.SongId, cancellationToken);
        if (songDevice != null && modifiedAt.HasValue)
        {
            songDevice.LastSyncedModifiedAt = modifiedAt.Value.ToUniversalTime();
        }
    }

    private async Task ProcessCreateLocalAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var modifiedAt = data?.ModifiedAt;

        var songDevice = await FindSongDeviceByIds(db, deviceId, record, data?.SongId, cancellationToken);
        if (songDevice != null)
        {
            songDevice.LastSyncedModifiedAt = (modifiedAt ?? DateTime.UtcNow).ToUniversalTime();
            songDevice.SyncAction = null;
            songDevice.SyncActionReason = null;
        }
    }

    private async Task ProcessUpdateLocalAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var modifiedAt = data?.ModifiedAt;

        var songDevice = await FindSongDeviceByIds(db, deviceId, record, data?.SongId, cancellationToken);
        if (songDevice != null)
        {
            songDevice.LastSyncedModifiedAt = (modifiedAt ?? DateTime.UtcNow).ToUniversalTime();
            songDevice.SyncAction = null;
            songDevice.SyncActionReason = null;
        }
    }

    private async Task ProcessDeleteLocalAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var songDevice = await FindSongDeviceByIds(db, deviceId, record, data?.SongId, cancellationToken);
        if (songDevice != null)
        {
            db.SongDevices.Remove(songDevice);
        }
    }

    private async Task ProcessLinkAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CommitImports imports, CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var songId = data?.SongId ?? record.SongId;
        var checksum = data?.Checksum;
        var modifiedAt = data?.ModifiedAt;

        // The song this Link targets gets its content from an import in this commit that failed
        if (checksum != null && imports.FailedChecksums.Contains(checksum))
        {
            await RecordError(db, sessionId, record,
                "Song to link was not imported: the import of the file with the same content failed", cancellationToken);
            return;
        }

        if ((!songId.HasValue || songId.Value <= 0) && checksum != null && imports.CreatedSongIds.TryGetValue(checksum, out var checksumSongId))
        {
            songId = checksumSongId;
        }

        if (!songId.HasValue || songId.Value <= 0)
            return;

        var songDevice = await musicService.AddSongsToDevice(db, deviceId, songId.Value, record.FilePath,
            (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);

        // A device file updated to another song's content is linked to that song: the device's
        // association at this path moves to it, and the previous song is left as it was
        if (songDevice != null && songDevice.SongId != songId)
        {
            songDevice.SongId = songId;
            songDevice.LastSyncedModifiedAt = (modifiedAt ?? DateTime.UtcNow).ToUniversalTime();
            songDevice.SyncAction = null;
            songDevice.SyncActionReason = null;
        }

        var song = await db.Songs.FindAsync([songId.Value], cancellationToken);
        logger.LogInformation("ProcessLinkAsync: path={Path}, songId={SongId}, song.FileModifiedAtTicks={SongFileModifiedAtTicks}, songDeviceIsNull={SongDeviceIsNull}, lastSyncedIsNull={LastSyncedIsNull}",
            record.FilePath, songId, song?.FileModifiedAt?.Ticks, songDevice == null, songDevice?.LastSyncedModifiedAt == null);

        // The sync rollback uses FileModifiedAt (the file-content change time), not ModifiedAt
        // (which also bumps on metadata-only edits). When the device's last-synced time is older
        // than the server's last file-content change, the song's FileModifiedAt is rolled back so
        // the device is not perpetually flagged as out-of-date. Null FileModifiedAt falls back to
        // ModifiedAt defensively (e.g. rows not yet backfilled).
        var songFileModifiedAt = song?.FileModifiedAt ?? song?.ModifiedAt;
        if (songDevice != null && song != null && songFileModifiedAt > songDevice.LastSyncedModifiedAt)
        {
            logger.LogInformation("ProcessLinkAsync: UPDATING song.FileModifiedAt from {OldValueTicks} to {NewValueTicks} for path={Path}",
                songFileModifiedAt.Value.Ticks, songDevice.LastSyncedModifiedAt.Value.Ticks, record.FilePath);
            song.FileModifiedAt = songDevice.LastSyncedModifiedAt.Value.ToUniversalTime();
        }
    }

    private async Task ProcessUnlinkAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var songDevice = await FindSongDeviceByIds(db, deviceId, record, data?.SongId, cancellationToken);
        if (songDevice != null)
        {
            db.SongDevices.Remove(songDevice);
        }
    }

    private async Task ProcessRenameAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<RenameData>(record.Data);
        if (data?.NewPath == null)
            return;

        var lookupPath = data.PreviousPath ?? record.FilePath;
        var songDevice = await db.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.DevicePath == lookupPath, cancellationToken);
        if (songDevice != null)
        {
            songDevice.DevicePath = data.NewPath;
            songDevice.SyncAction = null;
            songDevice.SyncActionReason = null;
        }
    }

    private Task ProcessSkippedAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        // Skipped actions should not, by definition, do anything. They only exist to record
        // that a file was observed and intentionally not mutated during the sync session
        return Task.CompletedTask;
    }

    private async Task ProcessUpdateTimestampAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<UpdateTimestampData>(record.Data);
        if (data == null)
            return;

        var newTimestamp = data.NewTimestamp;
        var songId = data.SongId ?? record.SongId;
        if (songId == null || songId <= 0)
            return;

        var songDevice = await db.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == songId, cancellationToken);
        if (songDevice != null)
        {
            songDevice.LastSyncedModifiedAt = newTimestamp.ToUniversalTime();
        }
    }

    /// <summary>
    /// SongDevices that orphan detection found before record processing: the orphans to unlink, and
    /// (for <see cref="SyncDirection.Up"/>) the SongDevices whose pending server actions are cleared.
    /// </summary>
    private sealed record OrphanDetectionResult(List<SongDevice> Orphans, List<SongDevice> PendingActionsToClear)
    {
        public static readonly OrphanDetectionResult None = new([], []);
    }

    private async Task<OrphanDetectionResult> DetectOrphansAsync(
        MusicDbContext db, long deviceId,
        List<DeviceSyncSessionRecord> records, SyncDirection direction,
        CancellationToken cancellationToken)
    {
        // Exclude Unlink: created by orphan detection itself, PendingActions (server-side), or CheckSync (Remove);
        // its FilePath does not correspond to a client-reported path.
        // Note: DeleteLocal records (server-initiated removal) ARE included in validFilePaths
        // so that the SongDevice being removed is not double-processed as an orphan.
        var validFilePaths = records
            .Where(r => r.Action != SyncRecordAction.Unlink)
            .Select(GetClientPath)
            .ToHashSet();

        if (direction == SyncDirection.Both)
        {
            var orphans = await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && sd.SyncAction == null
                             && !validFilePaths.Contains(sd.DevicePath))
                .Include(sd => sd.Song)
                .ToListAsync(cancellationToken);

            return new OrphanDetectionResult(orphans, []);
        }

        if (direction == SyncDirection.Up)
        {
            var orphans = await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && !validFilePaths.Contains(sd.DevicePath))
                .Include(sd => sd.Song)
                .ToListAsync(cancellationToken);

            var pendingActionsToClear = await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && validFilePaths.Contains(sd.DevicePath)
                             && sd.SyncAction != null)
                .ToListAsync(cancellationToken);

            return new OrphanDetectionResult(orphans, pendingActionsToClear);
        }

        return OrphanDetectionResult.None;
    }

    private static void HandleOrphans(
        MusicDbContext db, long sessionId, OrphanDetectionResult detection, bool isDryRun)
    {
        if (!isDryRun)
        {
            foreach (var sd in detection.PendingActionsToClear)
            {
                sd.SyncAction = null;
                sd.SyncActionReason = null;
            }
        }

        foreach (var sd in detection.Orphans)
        {
            var unlinkData = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = sd.SongId });
            var unlinkRecord = new DeviceSyncSessionRecord
            {
                SessionId = sessionId,
                FilePath = sd.DevicePath,
                Action = SyncRecordAction.Unlink,
                Data = unlinkData,
                SongId = sd.SongId,
                Reason = "Orphaned: path not present in sync session",
                ProcessedAt = DateTime.UtcNow,
            };
            db.DeviceSyncSessionRecords.Add(unlinkRecord);

            // Record processing may already have removed this SongDevice (and a mid-commit save
            // may have detached it); removing it again would fail the final save.
            if (!isDryRun && db.Entry(sd).State is not (EntityState.Deleted or EntityState.Detached))
            {
                db.SongDevices.Remove(sd);
            }
        }
    }

    /// <summary>
    /// Adds an <see cref="SyncRecordAction.Error"/> record for a record whose action could not be
    /// performed at commit. The caller skips the rest of that record's processing.
    /// </summary>
    private async Task RecordError(
        MusicDbContext db, long sessionId, DeviceSyncSessionRecord record, string errorMessage,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("Record {RecordId} in session {SessionId} failed at commit: {Error}",
            record.Id, sessionId, errorMessage);

        var errorData = SyncActionDataSerializer.Serialize(new ErrorData
        {
            ErrorMessage = errorMessage,
            FailedRecordId = record.Id,
        });
        var errorRecord = new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = record.FilePath,
            Action = SyncRecordAction.Error,
            Data = errorData,
            SongId = record.SongId,
            Reason = errorMessage,
            ProcessedAt = DateTime.UtcNow,
        };
        db.DeviceSyncSessionRecords.Add(errorRecord);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SongDevice?> FindSongDeviceByIds(
        MusicDbContext db, long deviceId, DeviceSyncSessionRecord record, long? dataSongId,
        CancellationToken cancellationToken)
    {
        var songId = dataSongId ?? record.SongId;
        if (songId.HasValue && songId.Value > 0)
        {
            var sd = await db.SongDevices
                .FirstOrDefaultAsync(sd2 => sd2.DeviceId == deviceId && sd2.SongId == songId, cancellationToken);
            if (sd != null)
                return sd;
        }

        return await db.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.DevicePath == record.FilePath, cancellationToken);
    }

    /// <summary>
    /// Returns the path of the SongDevice this record operates on. For Rename records, this is
    /// <see cref="RenameData.PreviousPath"/> (where the SongDevice currently lives); for all
    /// other actions, it is <see cref="DeviceSyncSessionRecord.FilePath"/>.
    /// </summary>
    private static string GetClientPath(DeviceSyncSessionRecord record)
    {
        if (record.Action == SyncRecordAction.Rename)
        {
            var data = SyncActionDataSerializer.Deserialize<RenameData>(record.Data)
                ?? throw new InvalidOperationException($"Rename record {record.Id} is missing Data.PreviousPath");
            return data.PreviousPath;
        }

        return record.FilePath;
    }

    /// <summary>
    /// Imports a staged file into the library. Import failures are returned instead of thrown, so
    /// the commit can record them as <see cref="SyncRecordAction.Error"/> records and continue.
    /// </summary>
    private async Task<ImportOutcome> ImportSongFromFile(
        MusicDbContext db, string tempFilePath, long? songId, long userId,
        DateTime? createdAt = null, DateTime? modifiedAt = null,
        string? originalFilePath = null,
        CancellationToken cancellationToken = default)
    {
        var job = new MusicImportJob(loggerFactory.CreateLogger<MusicImportJob>());
        var metadata = new SongImportMetadata(
            tempFilePath,
            (createdAt ?? DateTime.UtcNow).ToUniversalTime(),
            (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(),
            songId,
            originalFilePath);
        await musicService.ImportRepositorySongs(db, job, userId, [metadata],
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip,
            cancellationToken: cancellationToken);

        // ImportRepositorySongs catches per-song failures, rolls the song back and records them on the job
        var exception = job.Exceptions.FirstOrDefault();
        if (exception != null)
        {
            var detail = exception.InnerException != null
                ? $"{exception.Message}: {exception.InnerException.Message}"
                : exception.Message;
            return ImportOutcome.Failure($"Song import failed: {detail}");
        }

        if (songId.HasValue && songId.Value > 0)
        {
            return ImportOutcome.Success(songId.Value);
        }

        var importedSong = job.SongMapping.GetValueOrDefault(metadata);
        if (importedSong == null)
        {
            var skipReason = job.SkipReasons.FirstOrDefault();
            return ImportOutcome.Failure(skipReason != null
                ? $"Song import skipped: {skipReason.Message}"
                : "Song import returned no song and no skip reason");
        }

        return ImportOutcome.Success(importedSong.Id);
    }

    /// <summary>
    /// Result of <see cref="ImportSongFromFile"/>: the imported song's id, or why the import failed.
    /// </summary>
    private sealed record ImportOutcome(long? SongId, string? ErrorMessage)
    {
        public static ImportOutcome Success(long songId) => new(songId, null);
        public static ImportOutcome Failure(string errorMessage) => new(null, errorMessage);
    }

    /// <summary>
    /// Songs created by the commit's imports so far, by checksum, so later records with the same
    /// content link to them; and the checksums whose import failed, so those records report it.
    /// </summary>
    private sealed class CommitImports
    {
        public Dictionary<string, long> CreatedSongIds { get; } = [];
        public HashSet<string> FailedChecksums { get; } = [];

        public void MarkFailed(string? checksum)
        {
            if (checksum != null)
            {
                FailedChecksums.Add(checksum);
            }
        }
    }

    private static SyncCommitResult BuildExistingResult(
        DeviceSyncSession session, List<DeviceSyncSessionRecord> records)
    {
        return new SyncCommitResult
        {
            ActionCounts = records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count()),
            CommittedAt = session.CompletedAt ?? DateTime.UtcNow,
        };
    }

    private static SyncCommitResult BuildResult(
        List<DeviceSyncSessionRecord> records, DateTime committedAt)
    {
        return new SyncCommitResult
        {
            ActionCounts = records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count()),
            CommittedAt = committedAt,
        };
    }

    private static void AcknowledgeServerActionRecords(List<DeviceSyncSessionRecord> records)
    {
        foreach (var record in records)
        {
            if (!record.Acknowledged && !record.Action.IsClientAction())
            {
                record.Acknowledged = true;
            }
        }
    }

    /// <summary>
    /// Marks sync records as acknowledged by the client. For client-action types (CreateLocal,
    /// UpdateLocal, etc.), the <paramref name="modifiedAt"/> timestamp from the client's local
    /// filesystem is injected into the record's <see cref="SongModifiedAtData"/> payload. This
    /// timestamp is later used during <see cref="CommitAsync"/> to update
    /// <c>SongDevice.LastSyncedModifiedAt</c>, enabling accurate change detection in the next
    /// sync cycle.
    /// </summary>
    public static void AcknowledgeRecords(List<DeviceSyncSessionRecord> records, DateTime? modifiedAt)
    {
        foreach (var record in records)
        {
            record.Acknowledged = true;

            if (modifiedAt.HasValue && record.Action.IsClientAction())
            {
                var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
                if (data != null)
                {
                    data = data with { ModifiedAt = modifiedAt.Value.ToUniversalTime() };
                    record.Data = SyncActionDataSerializer.Serialize(data);
                }
            }
        }
    }

    public Task AcknowledgeRecordsAsync(List<DeviceSyncSessionRecord> records, DateTime? modifiedAt)
    {
        AcknowledgeRecords(records, modifiedAt);
        return Task.CompletedTask;
    }
}
