using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Songs;

namespace MyMusic.Common.Services.Sync;

public class SyncUploadService(
    MusicDbContext db,
    IFileSystem fileSystem,
    IMusicService musicService,
    ISongFileValidateService songFileValidate,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncSoundalikeMatcher soundalikeMatcher,
    ILogger<SyncUploadService> logger) : ISyncUploadService
{
    public async Task<SyncUploadResult> UploadAsync(
        long deviceId,
        long sessionId,
        bool isDryRun,
        string path,
        Stream fileStream,
        string fileName,
        DateTime modifiedAt,
        DateTime createdAt,
        bool isUpdate,
        SongDevice? songDeviceForImport,
        string repositoryPath,
        long ownerId,
        SyncDirection direction,
        bool deduplicate = false,
        long? resolvesConflictRecordId = null,
        CancellationToken cancellationToken = default)
    {
        var staging = await StageFileAsync(sessionId, fileStream, fileName, isDryRun, repositoryPath, cancellationToken);
        var keepStagedFile = false;

        try
        {
            var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
            var checksumAlgorithmName = checksumAlgorithm.GetType().Name;
            var checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, staging.StagedFilePath);

            long? songIdForRecord = isUpdate ? songDeviceForImport!.SongId!.Value : null;

            var (duplicateSongId, hasDuplicate, isPreviousVersion) = await FindDuplicateForUploadAsync(
                deviceId, sessionId, checksum, checksumAlgorithmName, ownerId, cancellationToken);

            var decision = DetermineUploadAction(
                isUpdate, hasDuplicate, duplicateSongId, isPreviousVersion,
                path, songIdForRecord, checksum, checksumAlgorithmName,
                modifiedAt, createdAt, songDeviceForImport);

            // Files that will be imported at commit are validated now, in both modes, so an
            // unimportable file becomes an Error record here instead of failing only a real commit
            var importError = decision.ActionType is SyncUploadActionType.CreateRemote or SyncUploadActionType.UpdateRemote
                ? await songFileValidate.ValidateAsync(staging.StagedFilePath, cancellationToken)
                : null;

            // A file that would be created on the server may sound like a song it already has (or like a
            // file uploaded earlier in this session): with deduplication, it is linked to that song instead.
            // A file that sounds like nothing is remembered (in memory only) for this session's later uploads.
            if (deduplicate && !isUpdate && importError == null && decision.ActionType == SyncUploadActionType.CreateRemote)
            {
                var match = await soundalikeMatcher.MatchOrRegisterAsync(
                    sessionId, ownerId, staging.StagedFilePath, checksum, path, cancellationToken);
                if (match != null)
                {
                    decision = await DetermineSoundalikeLinkAsync(match, checksum, checksumAlgorithmName, cancellationToken);
                }
            }

            var syncActions = syncActionsServerFactory.Create(db, sessionId, deviceId, isDryRun);
            var records = importError != null
                ? [await syncActions.ActionError(path, importError, songIdForRecord, reason: importError, cancellationToken: cancellationToken)]
                : await ExecuteDecisionAsync(decision, syncActions, path, staging, modifiedAt, createdAt, direction, cancellationToken);

            if (resolvesConflictRecordId.HasValue)
            {
                await LinkToResolvedConflictAsync(records, sessionId, path, resolvesConflictRecordId.Value, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);

            // In a real run, a file that will be imported must outlive this request: the commit imports it
            // from the record's TempFilePath. Every other staged file (dry runs, links, errors) is never
            // read again, so it is deleted when the request ends. The session directory itself, with any
            // leftovers, is deleted when the session ends (see StagingDirectoryCleanupService).
            keepStagedFile = !isDryRun && importError == null
                && decision.ActionType is SyncUploadActionType.CreateRemote or SyncUploadActionType.UpdateRemote;

            long? effectiveSongId = decision.ActionType switch
            {
                SyncUploadActionType.LinkWithSongId or SyncUploadActionType.LinkWithChecksumOnly
                    or SyncUploadActionType.LinkWithSongIdAndUpdateLocal
                    or SyncUploadActionType.SoundalikeLink => decision.SongId,
                _ => duplicateSongId ?? songIdForRecord,
            };

            return new SyncUploadResult
            {
                Records = records,
                EffectiveSongId = effectiveSongId,
            };
        }
        finally
        {
            if (!keepStagedFile)
            {
                TryDeleteStagedFile(staging.StagedFilePath);
            }
        }
    }

    /// <summary>
    /// Points the records of an upload to the conflict the user resolved by keeping the local file. An
    /// upload that produced an error leaves the conflict unresolved, and so does an id that is not a
    /// conflict of this session at this path.
    /// </summary>
    private async Task LinkToResolvedConflictAsync(
        List<DeviceSyncSessionRecord> records, long sessionId, string path, long conflictRecordId,
        CancellationToken cancellationToken)
    {
        if (records.Any(r => r.Action == SyncRecordAction.Error)) return;

        var isConflict = await db.DeviceSyncSessionRecords.AnyAsync(
            r => r.Id == conflictRecordId && r.SessionId == sessionId
                 && r.Action == SyncRecordAction.Conflict && r.FilePath == path,
            cancellationToken);

        if (!isConflict)
        {
            logger.LogWarning("Record {ConflictRecordId} is not a conflict of session {SessionId} at {Path}", conflictRecordId, sessionId, path);
            return;
        }

        foreach (var record in records)
        {
            record.ResolvesConflictRecordId = conflictRecordId;
        }
    }

    private record StagingResult(string StagedFilePath, bool IsDryRun);

    /// <summary>
    /// Stages the uploaded file in the session's staging directory, <c>.temp/sync-{sessionId}</c> inside
    /// the repository. Dry runs use the same directory, so a single cleanup covers both modes.
    /// </summary>
    private async Task<StagingResult> StageFileAsync(
        long sessionId, Stream fileStream, string fileName,
        bool isDryRun, string repositoryPath, CancellationToken cancellationToken)
    {
        var stagingDirectory = fileSystem.Path.Combine(repositoryPath, ".temp", $"sync-{sessionId}");
        fileSystem.Directory.CreateDirectory(stagingDirectory);
        var stagedFilePath = fileSystem.Path.Combine(stagingDirectory, $"{Guid.NewGuid()}-{fileName}");
        await using (var stream = fileSystem.FileStream.New(stagedFilePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(stream, cancellationToken);
        }
        return new StagingResult(stagedFilePath, isDryRun);
    }

    private SyncUploadDecision DetermineUploadAction(
        bool isUpdate,
        bool hasDuplicate, long? duplicateSongId, bool isPreviousVersion,
        string path, long? songIdForRecord,
        string checksum, string algorithm,
        DateTime modifiedAt, DateTime createdAt,
        SongDevice? songDeviceForImport)
    {
        // A file holding a previous version of a song is never imported: the server's current version
        // wins, so the device downloads it (the file is first linked to that song, unless it already is)
        if (hasDuplicate && isPreviousVersion && duplicateSongId.HasValue)
        {
            var isOwnSong = isUpdate && duplicateSongId == songDeviceForImport!.SongId;

            return new SyncUploadDecision
            {
                ActionType = isOwnSong
                    ? SyncUploadActionType.UpdateLocal
                    : SyncUploadActionType.LinkWithSongIdAndUpdateLocal,
                SongId = duplicateSongId.Value,
                Checksum = checksum,
                ChecksumAlgorithm = algorithm,
                Reason = isOwnSong ? "File matches a previous version of its song, server version wins"
                    : isUpdate ? "Linked to a different song (file matches a previous version of it)"
                    : "Linked to existing song (file matches a previous version of it)",
            };
        }

        // An updated file whose content now belongs to another song is linked to that song below.
        // Importing it over its own song would make the commit merge the two songs, which no record
        // would describe.
        if (isUpdate && (!hasDuplicate || duplicateSongId == songDeviceForImport!.SongId))
        {
            return new SyncUploadDecision
            {
                ActionType = SyncUploadActionType.UpdateRemote,
                SongId = songDeviceForImport!.SongId,
                Checksum = checksum,
                ChecksumAlgorithm = algorithm,
                Reason = "File re-uploaded (updated)",
            };
        }

        var linkReason = isUpdate
            ? "Linked to existing song (updated file duplicates it)"
            : "Linked to existing song (duplicate checksum)";

        if (hasDuplicate && duplicateSongId.HasValue)
        {
            return new SyncUploadDecision
            {
                ActionType = SyncUploadActionType.LinkWithSongId,
                SongId = duplicateSongId.Value,
                Checksum = checksum,
                ChecksumAlgorithm = algorithm,
                Reason = linkReason,
            };
        }

        if (hasDuplicate)
        {
            return new SyncUploadDecision
            {
                ActionType = SyncUploadActionType.LinkWithChecksumOnly,
                Checksum = checksum,
                ChecksumAlgorithm = algorithm,
                Reason = linkReason,
            };
        }

        return new SyncUploadDecision
        {
            ActionType = SyncUploadActionType.CreateRemote,
            SongId = songIdForRecord,
            Checksum = checksum,
            ChecksumAlgorithm = algorithm,
            Reason = "New file uploaded",
        };
    }

    /// <summary>
    /// Links a new file to its soundalike. The Link's checksum is the content the linked song will have: the
    /// library song's, or the checksum of the session upload whose song the commit creates.
    /// </summary>
    private async Task<SyncUploadDecision> DetermineSoundalikeLinkAsync(
        SyncSoundalikeMatch match, string checksum, string algorithm, CancellationToken cancellationToken)
    {
        if (match.SongId is { } songId)
        {
            var song = await db.Songs
                .Where(s => s.Id == songId)
                .Select(s => new { s.Checksum, s.ChecksumAlgorithm })
                .FirstAsync(cancellationToken);

            return new SyncUploadDecision
            {
                ActionType = SyncUploadActionType.SoundalikeLink,
                SongId = songId,
                Checksum = song.Checksum,
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                LocalChecksum = checksum,
                Reason = $"Linked to existing song (soundalike, score {match.Score:F2})",
            };
        }

        return new SyncUploadDecision
        {
            ActionType = SyncUploadActionType.SoundalikeLink,
            Checksum = match.UploadChecksum,
            ChecksumAlgorithm = algorithm,
            LocalChecksum = checksum,
            LocalSourcePath = match.UploadPath,
            Reason = $"Linked to the song of '{match.UploadPath}' uploaded in this session (soundalike, score {match.Score:F2})",
        };
    }

    private async Task<List<DeviceSyncSessionRecord>> ExecuteDecisionAsync(
        SyncUploadDecision decision,
        ISyncActionsServer syncActions,
        string path,
        StagingResult staging,
        DateTime modifiedAt,
        DateTime createdAt,
        SyncDirection direction,
        CancellationToken cancellationToken)
    {
        string? tempFilePath = staging.IsDryRun ? null : staging.StagedFilePath;

        switch (decision.ActionType)
        {
            case SyncUploadActionType.UpdateRemote:
                return
                [
                    await syncActions.ActionUpdateRemote(
                        path, decision.SongId, decision.Checksum!, decision.ChecksumAlgorithm!,
                        modifiedAt, tempFilePath, createdAt, path,
                        decision.Reason, cancellationToken),
                ];

            case SyncUploadActionType.LinkWithSongId:
                return
                [
                    await syncActions.ActionLink(
                        path, decision.SongId!.Value, modifiedAt,
                        decision.Checksum, decision.ChecksumAlgorithm,
                        decision.Reason, cancellationToken: cancellationToken),
                ];

            case SyncUploadActionType.LinkWithChecksumOnly:
                return
                [
                    await syncActions.ActionLink(
                        path, decision.Checksum!, decision.ChecksumAlgorithm!, modifiedAt,
                        decision.Reason, cancellationToken),
                ];

            case SyncUploadActionType.CreateRemote:
                return
                [
                    await syncActions.ActionCreateRemote(
                        path, decision.SongId, decision.Checksum!, decision.ChecksumAlgorithm!,
                        modifiedAt, tempFilePath, createdAt, path,
                        decision.Reason, cancellationToken),
                ];

            case SyncUploadActionType.UpdateLocal:
                return [await ActionUpdateLocalOrSkippedAsync(decision, syncActions, path, direction, cancellationToken)];

            case SyncUploadActionType.LinkWithSongIdAndUpdateLocal:
            {
                var linkRecord = await syncActions.ActionLink(
                    path, decision.SongId!.Value, modifiedAt,
                    decision.Checksum, decision.ChecksumAlgorithm,
                    decision.Reason, isPreviousVersion: true, cancellationToken);
                var updateLocalRecord = await ActionUpdateLocalOrSkippedAsync(decision, syncActions, path, direction, cancellationToken);

                return [linkRecord, updateLocalRecord];
            }

            case SyncUploadActionType.SoundalikeLink:
            {
                var linkRecord = await syncActions.ActionSoundalikeLink(
                    path, decision.SongId, decision.Checksum!, decision.ChecksumAlgorithm!, decision.LocalChecksum!,
                    modifiedAt, decision.Reason, cancellationToken);

                // The song of a session upload only exists after the commit, so the device copies the
                // uploaded file it sounds like, instead of downloading the song
                var updateLocalRecord = decision.SongId.HasValue
                    ? await ActionUpdateLocalOrSkippedAsync(decision, syncActions, path, direction, cancellationToken)
                    : direction == SyncDirection.Up
                        ? await syncActions.ActionSkipped(path,
                            reason: "File sounds like a file uploaded in this session, not replaced (direction up)",
                            cancellationToken: cancellationToken)
                        : await syncActions.ActionUpdateLocalFromLocalFile(path, decision.LocalSourcePath!,
                            $"File sounds like '{decision.LocalSourcePath}' uploaded in this session, replaced by it",
                            cancellationToken);

                return [linkRecord, updateLocalRecord];
            }

            default:
                throw new InvalidOperationException($"Unknown upload action type: {decision.ActionType}");
        }
    }

    /// <summary>
    /// Records the download of the song's current file over a device file holding a previous version of it (or
    /// a soundalike of it), or a <c>Skipped</c> record in <c>up</c> direction, where the device is never changed.
    /// </summary>
    private async Task<DeviceSyncSessionRecord> ActionUpdateLocalOrSkippedAsync(
        SyncUploadDecision decision,
        ISyncActionsServer syncActions,
        string path,
        SyncDirection direction,
        CancellationToken cancellationToken)
    {
        var songId = decision.SongId!.Value;

        var isSoundalike = decision.ActionType == SyncUploadActionType.SoundalikeLink;

        if (direction == SyncDirection.Up)
        {
            return await syncActions.ActionSkipped(path, songId,
                isSoundalike
                    ? "File sounds like the song, not downloaded (direction up)"
                    : "File matches a previous version of the song, not downloaded (direction up)",
                cancellationToken);
        }

        var song = await db.Songs.FirstAsync(s => s.Id == songId, cancellationToken);
        var songFileModifiedAt = song.FileModifiedAt ?? song.ModifiedAt;

        return await syncActions.ActionUpdateLocal(path, song.Id, songFileModifiedAt,
            isSoundalike
                ? $"File sounds like the song, replaced by the server file (modified at {songFileModifiedAt:O})"
                : $"File matches a previous version of the song, server modified at {songFileModifiedAt:O} wins",
            cancellationToken);
    }

    /// <summary>
    /// Finds the song whose content the uploaded file has: first among this session's records, then in the
    /// user's library. <c>IsPreviousVersion</c> is set when the file matches only an older version of a
    /// library song; session records always hold the content their song will have.
    /// </summary>
    private async Task<(long? SongId, bool HasDuplicate, bool IsPreviousVersion)> FindDuplicateForUploadAsync(
        long deviceId, long sessionId, string checksum, string checksumAlgorithm,
        long ownerId, CancellationToken cancellationToken)
    {
        // Pending UpdateRemote records count too: the commit gives their song this content
        var sessionRecords = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId
                      && (r.Action == SyncRecordAction.CreateRemote
                          || r.Action == SyncRecordAction.UpdateRemote
                          || r.Action == SyncRecordAction.Link))
            .ToListAsync(cancellationToken);

        long? matchedSongId = null;
        bool checksumFound = false;

        foreach (var r in sessionRecords)
        {
            if (r.Data == null) continue;
            var recordChecksum = ExtractChecksumFromRecord(r);
            if (recordChecksum != checksum) continue;

            checksumFound = true;
            var songId = ExtractSongIdFromRecord(r);
            if (songId.HasValue && songId.Value > 0)
                return (songId.Value, true, false);

            matchedSongId ??= songId;
        }

        if (checksumFound)
            return (matchedSongId, true, false);

        var existingSongs = await musicService.FindUserSongsByChecksum(
            db, ownerId, [checksum], checksumAlgorithm, cancellationToken);

        if (existingSongs.TryGetValue(checksum, out var existingSong))
            return (existingSong.Id, true, existingSong.Checksum != checksum);

        return (null, false, false);
    }

    private static string? ExtractChecksumFromRecord(DeviceSyncSessionRecord r)
    {
        var data = r.Action == SyncRecordAction.CreateRemote
            ? (SyncActionDataSerializer.Deserialize<CreateRemoteData>(r.Data) as object ??
               SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data))
            : SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data);

        return data switch
        {
            CreateRemoteData crd => crd.Checksum,
            SongModifiedAtData smd => smd.Checksum,
            _ => null
        };
    }

    private static long? ExtractSongIdFromRecord(DeviceSyncSessionRecord r)
    {
        var data = r.Action == SyncRecordAction.CreateRemote
            ? (SyncActionDataSerializer.Deserialize<CreateRemoteData>(r.Data) as object ??
               SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data))
            : SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data);

        return data switch
        {
            CreateRemoteData crd => crd.SongId,
            SongModifiedAtData smd => smd.SongId,
            _ => null
        };
    }

    private void TryDeleteStagedFile(string filePath)
    {
        try
        {
            if (fileSystem.File.Exists(filePath))
                fileSystem.File.Delete(filePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete staged file {FilePath}", filePath);
        }
    }
}