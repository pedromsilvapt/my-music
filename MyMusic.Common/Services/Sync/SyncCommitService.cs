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
        string direction = "both", CancellationToken cancellationToken = default)
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

        if (!isDryRun)
        {
            var unacknowledgedClientActions = records
                .Where(r => !r.Acknowledged && IsClientActionType(r.Action))
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
        }

        AcknowledgeServerActionRecords(records);

        var device = await db.Devices.FirstAsync(d => d.Id == deviceId, cancellationToken);
        var userId = device.OwnerId;

        var createdSongIdsByChecksum = new Dictionary<string, long>();

        foreach (var record in records)
        {
            await ProcessRecordAsync(db, sessionId, deviceId, record, isDryRun, userId, createdSongIdsByChecksum, cancellationToken);
        }

        await DetectAndHandleOrphansAsync(db, sessionId, deviceId, records, isDryRun, direction, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var committedAt = DateTime.UtcNow;

        return BuildResult(await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(cancellationToken), committedAt);
    }

    private async Task ProcessRecordAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        long userId, Dictionary<string, long> createdSongIdsByChecksum, CancellationToken cancellationToken)
    {
        switch (record.Action)
        {
            case SyncRecordAction.CreateRemote:
                await ProcessCreateRemoteAsync(db, sessionId, deviceId, record, isDryRun, userId, createdSongIdsByChecksum, cancellationToken);
                break;
            case SyncRecordAction.UpdateRemote:
                await ProcessUpdateRemoteAsync(db, sessionId, deviceId, record, isDryRun, userId, createdSongIdsByChecksum, cancellationToken);
                break;
            case SyncRecordAction.CreateLocal:
                await ProcessCreateLocalAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.UpdateLocal:
                await ProcessUpdateLocalAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Delete:
                await ProcessDeleteAsync(db, sessionId, deviceId, record, isDryRun, cancellationToken);
                break;
            case SyncRecordAction.Link:
                await ProcessLinkAsync(db, sessionId, deviceId, record, isDryRun, createdSongIdsByChecksum, cancellationToken);
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
        long userId, Dictionary<string, long> createdSongIdsByChecksum, CancellationToken cancellationToken)
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
                await ImportSongFromFile(db, tempFilePath, songId.Value, userId, originalFilePath: originalFilePath, cancellationToken: cancellationToken);
                songDevice = await musicService.AddSongsToDevice(db, deviceId, songId.Value, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else if (checksum != null && createdSongIdsByChecksum.TryGetValue(checksum, out var existingSongId))
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, existingSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else
            {
                var newSongId = await ImportSongFromFile(db, tempFilePath, null, userId, createdAt, modifiedAt, originalFilePath, cancellationToken);
                if (checksum != null)
                {
                    createdSongIdsByChecksum[checksum] = newSongId;
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
            else if (checksum != null && createdSongIdsByChecksum.TryGetValue(checksum, out var existingSongId))
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, existingSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
        }

        var songAfterCreate = songId.HasValue && songId.Value > 0
            ? await db.Songs.FindAsync([songId.Value], cancellationToken)
            : null;
        logger.LogInformation("ProcessCreateRemoteAsync: path={Path}, songId={SongId}, song.ModifiedAtTicks={SongModifiedAtTicks}, sdIsNull={SdIsNull}, lastSyncedIsNull={LastSyncedIsNull}",
            record.FilePath, songId, songAfterCreate?.ModifiedAt.Ticks, songDevice == null, songDevice?.LastSyncedModifiedAt == null);
    }

    private async Task ProcessUpdateRemoteAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        long userId, Dictionary<string, long> createdSongIdsByChecksum, CancellationToken cancellationToken)
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
                await ImportSongFromFile(db, tempFilePath, songId.Value, userId, originalFilePath: originalFilePath, cancellationToken: cancellationToken);
            }
            else if (checksum != null && createdSongIdsByChecksum.TryGetValue(checksum, out var existingSongId))
            {
                songDevice = await musicService.AddSongsToDevice(db, deviceId, existingSongId, record.FilePath,
                    (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);
            }
            else
            {
                var newSongId = await ImportSongFromFile(db, tempFilePath, null, userId, createdAt, modifiedAt, originalFilePath, cancellationToken);
                if (checksum != null)
                {
                    createdSongIdsByChecksum[checksum] = newSongId;
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

    private async Task ProcessDeleteAsync(
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
        Dictionary<string, long> createdSongIdsByChecksum, CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var songId = data?.SongId ?? record.SongId;
        var checksum = data?.Checksum;
        var modifiedAt = data?.ModifiedAt;

        if ((!songId.HasValue || songId.Value <= 0) && checksum != null && createdSongIdsByChecksum.TryGetValue(checksum, out var checksumSongId))
        {
            songId = checksumSongId;
        }

        if (!songId.HasValue || songId.Value <= 0)
            return;

        var songDevice = await musicService.AddSongsToDevice(db, deviceId, songId.Value, record.FilePath,
            (modifiedAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);

        var song = await db.Songs.FindAsync([songId.Value], cancellationToken);
        logger.LogInformation("ProcessLinkAsync: path={Path}, songId={SongId}, song.ModifiedAtTicks={SongModifiedAtTicks}, songDeviceIsNull={SongDeviceIsNull}, lastSyncedIsNull={LastSyncedIsNull}",
            record.FilePath, songId, song?.ModifiedAt.Ticks, songDevice == null, songDevice?.LastSyncedModifiedAt == null);
        if (songDevice != null && song != null && song.ModifiedAt > songDevice.LastSyncedModifiedAt)
        {
            logger.LogInformation("ProcessLinkAsync: UPDATING song.ModifiedAt from {OldValueTicks} to {NewValueTicks} for path={Path}",
                song.ModifiedAt.Ticks, songDevice.LastSyncedModifiedAt.Value.Ticks, record.FilePath);
            song.ModifiedAt = songDevice.LastSyncedModifiedAt.Value.ToUniversalTime();
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

    private async Task ProcessSkippedAsync(
        MusicDbContext db, long sessionId, long deviceId, DeviceSyncSessionRecord record, bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
            return;

        var data = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data);
        var modifiedAt = data?.ModifiedAt;
        if (modifiedAt == null)
            return;

        var songDevice = await FindSongDeviceByIds(db, deviceId, record, data?.SongId, cancellationToken);
        if (songDevice != null)
        {
            songDevice.LastSyncedModifiedAt = modifiedAt.Value.ToUniversalTime();
        }
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

    private async Task DetectAndHandleOrphansAsync(
        MusicDbContext db, long sessionId, long deviceId,
        List<DeviceSyncSessionRecord> records, bool isDryRun, string direction,
        CancellationToken cancellationToken)
    {
        var validFilePaths = records
            .Where(r => r.Action is SyncRecordAction.CreateRemote or SyncRecordAction.UpdateRemote
                or SyncRecordAction.Skipped or SyncRecordAction.CreateLocal or SyncRecordAction.UpdateLocal
                or SyncRecordAction.Link or SyncRecordAction.Error)
            .Select(r => r.FilePath)
            .ToHashSet();

        List<SongDevice> orphanedSongDevices;

        if (direction == "both")
        {
            orphanedSongDevices = await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && sd.SyncAction == null
                             && !validFilePaths.Contains(sd.DevicePath))
                .Include(sd => sd.Song)
                .ToListAsync(cancellationToken);
        }
        else if (direction == "up")
        {
            orphanedSongDevices = await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && !validFilePaths.Contains(sd.DevicePath))
                .Include(sd => sd.Song)
                .ToListAsync(cancellationToken);

            if (!isDryRun)
            {
                var songsToClear = await db.SongDevices
                    .Where(sd => sd.DeviceId == deviceId
                                 && validFilePaths.Contains(sd.DevicePath)
                                 && sd.SyncAction != null)
                    .ToListAsync(cancellationToken);

                foreach (var sd in songsToClear)
                {
                    sd.SyncAction = null;
                    sd.SyncActionReason = null;
                }
            }
        }
        else
        {
            return;
        }

        if (orphanedSongDevices.Count == 0)
            return;

        foreach (var sd in orphanedSongDevices)
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

            if (!isDryRun)
            {
                db.SongDevices.Remove(sd);
            }
        }
    }

    private async Task RecordError(
        MusicDbContext db, long sessionId, DeviceSyncSessionRecord record, string errorMessage,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("Staged file missing for record {RecordId} in session {SessionId}: {Error}",
            record.Id, sessionId, errorMessage);

        var errorData = SyncActionDataSerializer.Serialize(new ErrorData { ErrorMessage = errorMessage });
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

    private async Task<long> ImportSongFromFile(
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

        if (songId.HasValue && songId.Value > 0)
        {
            return songId.Value;
        }

        var importedSong = job.SongMapping.GetValueOrDefault(metadata);
        if (importedSong == null)
        {
            var skipReason = job.SkipReasons.FirstOrDefault();
            var skipMessage = skipReason != null
                ? $"Song import skipped: {skipReason.Message}"
                : "Song import returned no song and no skip reason";
            throw new InvalidOperationException(skipMessage);
        }

        return importedSong.Id;
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

    private static bool IsClientActionType(SyncRecordAction action) =>
        action is SyncRecordAction.CreateLocal or SyncRecordAction.UpdateLocal
            or SyncRecordAction.Unlink or SyncRecordAction.Rename or SyncRecordAction.Delete;

    private static void AcknowledgeServerActionRecords(List<DeviceSyncSessionRecord> records)
    {
        foreach (var record in records)
        {
            if (!record.Acknowledged && !IsClientActionType(record.Action))
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

            if (modifiedAt.HasValue && IsClientActionType(record.Action))
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
