using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public class SyncUploadService(
    MusicDbContext db,
    IFileSystem fileSystem,
    IMusicService musicService,
    ISyncActionsServerFactory syncActionsServerFactory,
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
        CancellationToken cancellationToken = default)
    {
        var staging = await StageFileAsync(sessionId, fileStream, fileName, isDryRun, repositoryPath, cancellationToken);

        try
        {
            var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
            var checksumAlgorithmName = checksumAlgorithm.GetType().Name;
            var checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, staging.StagedFilePath!);

            long? songIdForRecord = isUpdate ? songDeviceForImport!.SongId!.Value : null;

            var (duplicateSongId, hasDuplicate) = await FindDuplicateForUploadAsync(
                deviceId, sessionId, checksum, checksumAlgorithmName, ownerId, cancellationToken);

            var decision = DetermineUploadAction(
                isUpdate, hasDuplicate, duplicateSongId,
                path, songIdForRecord, checksum, checksumAlgorithmName,
                modifiedAt, createdAt, songDeviceForImport);

            var syncActions = syncActionsServerFactory.Create(db, sessionId, deviceId, isDryRun);
            var record = await ExecuteDecisionAsync(decision, syncActions, path, staging, modifiedAt, createdAt, cancellationToken);

            if (!isDryRun && (decision.ActionType == SyncUploadActionType.LinkWithSongId
                           || decision.ActionType == SyncUploadActionType.LinkWithChecksumOnly))
            {
                TryDeleteStagedFile(staging.StagedFilePath!);
            }

            await db.SaveChangesAsync(cancellationToken);

            long? effectiveSongId = decision.ActionType switch
            {
                SyncUploadActionType.LinkWithSongId => decision.SongId,
                _ => duplicateSongId ?? songIdForRecord,
            };

            return new SyncUploadResult
            {
                Record = record,
                EffectiveSongId = effectiveSongId,
            };
        }
        finally
        {
            if (staging.IsDryRun && staging.StagingDirectory != null)
            {
                TryDeleteStagingDirectory(staging.StagingDirectory);
            }
        }
    }

    private record StagingResult(string StagedFilePath, string? StagingDirectory, bool IsDryRun);

    private async Task<StagingResult> StageFileAsync(
        long sessionId, Stream fileStream, string fileName,
        bool isDryRun, string repositoryPath, CancellationToken cancellationToken)
    {
        if (isDryRun)
        {
            var systemTempPath = fileSystem.Path.Combine(
                fileSystem.Path.GetTempPath(), $"mymusic_staging_dryrun_{Guid.NewGuid()}");
            fileSystem.Directory.CreateDirectory(systemTempPath);
            var tempFilePath = fileSystem.Path.Combine(systemTempPath, fileName);
            await using (var stream = fileSystem.FileStream.New(tempFilePath, FileMode.Create))
            {
                await fileStream.CopyToAsync(stream, cancellationToken);
            }
            return new StagingResult(tempFilePath, systemTempPath, IsDryRun: true);
        }
        else
        {
            var tempPath = fileSystem.Path.Combine(repositoryPath, ".temp", $"sync-{sessionId}");
            fileSystem.Directory.CreateDirectory(tempPath);
            var stagingFileName = $"{Guid.NewGuid()}-{fileName}";
            var stagingFilePath = fileSystem.Path.Combine(tempPath, stagingFileName);
            await using (var stream = fileSystem.FileStream.New(stagingFilePath, FileMode.Create))
            {
                await fileStream.CopyToAsync(stream, cancellationToken);
            }
            return new StagingResult(stagingFilePath, tempPath, IsDryRun: false);
        }
    }

    private SyncUploadDecision DetermineUploadAction(
        bool isUpdate,
        bool hasDuplicate, long? duplicateSongId,
        string path, long? songIdForRecord,
        string checksum, string algorithm,
        DateTime modifiedAt, DateTime createdAt,
        SongDevice? songDeviceForImport)
    {
        if (isUpdate)
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

        if (hasDuplicate && duplicateSongId.HasValue)
        {
            return new SyncUploadDecision
            {
                ActionType = SyncUploadActionType.LinkWithSongId,
                SongId = duplicateSongId.Value,
                Checksum = checksum,
                ChecksumAlgorithm = algorithm,
                Reason = "Linked to existing song (duplicate checksum)",
            };
        }

        if (hasDuplicate)
        {
            return new SyncUploadDecision
            {
                ActionType = SyncUploadActionType.LinkWithChecksumOnly,
                Checksum = checksum,
                ChecksumAlgorithm = algorithm,
                Reason = "Linked to existing song (duplicate checksum)",
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

    private async Task<DeviceSyncSessionRecord> ExecuteDecisionAsync(
        SyncUploadDecision decision,
        ISyncActionsServer syncActions,
        string path,
        StagingResult staging,
        DateTime modifiedAt,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        string? tempFilePath = staging.IsDryRun ? null : staging.StagedFilePath;

        return decision.ActionType switch
        {
            SyncUploadActionType.UpdateRemote =>
                await syncActions.ActionUpdateRemote(
                    path, decision.SongId, decision.Checksum!, decision.ChecksumAlgorithm!,
                    modifiedAt, tempFilePath, createdAt, tempFilePath,
                    decision.Reason, cancellationToken),

            SyncUploadActionType.LinkWithSongId =>
                await syncActions.ActionLink(
                    path, decision.SongId!.Value, modifiedAt,
                    decision.Checksum, decision.ChecksumAlgorithm,
                    decision.Reason, cancellationToken),

            SyncUploadActionType.LinkWithChecksumOnly =>
                await syncActions.ActionLink(
                    path, decision.Checksum!, decision.ChecksumAlgorithm!, modifiedAt,
                    decision.Reason, cancellationToken),

            SyncUploadActionType.CreateRemote =>
                await syncActions.ActionCreateRemote(
                    path, decision.SongId, decision.Checksum!, decision.ChecksumAlgorithm!,
                    modifiedAt, tempFilePath, createdAt, tempFilePath,
                    decision.Reason, cancellationToken),

            _ => throw new InvalidOperationException($"Unknown upload action type: {decision.ActionType}")
        };
    }

    private async Task<(long? SongId, bool HasDuplicate)> FindDuplicateForUploadAsync(
        long deviceId, long sessionId, string checksum, string checksumAlgorithm,
        long ownerId, CancellationToken cancellationToken)
    {
        var sessionRecords = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId
                      && (r.Action == SyncRecordAction.CreateRemote || r.Action == SyncRecordAction.Link))
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
                return (songId.Value, true);

            matchedSongId ??= songId;
        }

        if (checksumFound)
            return (matchedSongId, true);

        var existingSongs = await musicService.FindUserSongsByChecksum(
            db, ownerId, [checksum], checksumAlgorithm, cancellationToken);

        if (existingSongs.TryGetValue(checksum, out var existingSong))
            return (existingSong.Id, true);

        return (null, false);
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

    private void TryDeleteStagingDirectory(string directoryPath)
    {
        try
        {
            if (fileSystem.Directory.Exists(directoryPath))
                fileSystem.Directory.Delete(directoryPath, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete staging directory {DirectoryPath}", directoryPath);
        }
    }
}