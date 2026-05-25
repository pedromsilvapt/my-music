namespace MyMusic.CLI.Services.Sync;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync.Types;

public class Phases(
    ISyncApiClient apiClient,
    SyncActionsDevice syncActions,
    ISyncConfig config,
    IFileSystemScanner scanner,
    ILogger<Phases> logger)
{
    public async Task<ScanResult> ScanPhaseAsync(
        SyncContext ctx,
        IProgress<SyncProgress>? progress,
        CancellationToken ct = default)
    {
        logger.LogInformation("Scanning repository: {Path}", ctx.RepositoryPath);

        progress?.Report(SyncProgress.ForPhase("scanning", "Scanning your music folder..."));

        var scanErrors = new List<ScanError>();

        var result = await scanner.ScanAsync(
            ctx.RepositoryPath,
            config.GetMusicExtensions(),
            config.GetExcludePatterns(),
            onProgress: (scannedCount, currentDir) =>
            {
                progress?.Report(SyncProgress.ForScanProgress(scannedCount, currentDir));
            },
            onError: (path, error) =>
            {
                scanErrors.Add(new ScanError { Path = path, Error = error });
            },
            ct);

        logger.LogInformation("Found {Count} music files", result.Files.Count);

        if (scanErrors.Count > 0)
        {
            logger.LogWarning("Scan completed with {Count} errors", scanErrors.Count);
            ctx.Result = ctx.Result.AddDelta(new SyncActionCounts { ErrorCount = scanErrors.Count });
        }

        return result;
    }

    public async Task StartSessionAsync(
        SyncContext ctx,
        List<ScanError> scanErrors,
        CancellationToken ct = default)
    {
        var startResponse = await apiClient.StartSyncAsync(ctx.DeviceId, new StartSyncRequest
        {
            DryRun = ctx.Options.DryRun,
            RepositoryPath = ctx.RepositoryPath,
            ScanErrors = scanErrors
        }, ct);
        ctx.SessionId = startResponse.SessionId;
        logger.LogInformation("Started sync session: {SessionId} (DryRun: {DryRun})", ctx.SessionId, ctx.Options.DryRun);
    }

    public async Task UploadPhaseAsync(
        SyncContext ctx,
        List<ScannedFile> files,
        IProgress<SyncProgress>? progress,
        CancellationToken ct = default)
    {
        if (ctx.Options.Direction == SyncDirection.Down)
        {
            logger.LogInformation("Skipping upload phase (direction: down)");
            return;
        }

        if (files.Count == 0)
        {
            return;
        }

        var chunkSize = config.GetChunkSize();
        var chunks = files.Chunk(chunkSize).ToList();
        logger.LogInformation("Processing {ChunkCount} chunks", chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var chunk = chunks[i];
            var chunkNumber = i + 1;
            logger.LogInformation("Processing chunk {ChunkNumber}/{TotalChunks}", chunkNumber, chunks.Count);

            var syncFiles = chunk
                .Select(f => new SyncFileInfo
                {
                    Path = f.RelativePath,
                    ModifiedAt = f.ModifiedAt,
                    CreatedAt = f.CreatedAt
                }).ToList();

            var syncRequest = new CheckSyncRequest
            {
                Files = syncFiles,
                Force = ctx.Options.Force
            };

            CheckSyncResult syncResponse;
            try
            {
                syncResponse = await apiClient.CheckSyncAsync(ctx.DeviceId, ctx.SessionId, syncRequest, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check sync for chunk {ChunkNumber}", chunkNumber);
                ctx.Result = ctx.Result.AddDelta(new SyncActionCounts { ErrorCount = chunk.Length });
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, (i + 1) * chunkSize,
                    errorMessage: $"Chunk {chunkNumber} failed: {ex.Message}"));
                continue;
            }

            ctx.Result = ctx.Result.AddDelta(syncResponse.Counts);

            if (syncResponse.Records.Count > 0)
            {
                ctx.PendingServerRecords.AddRange(syncResponse.Records);
                logger.LogInformation(
                    "Chunk {ChunkNumber}: {RecordCount} server action records accumulated",
                    chunkNumber, syncResponse.Records.Count);
            }

            var toCreatePaths = syncResponse.ToCreate.Select(f => f.Path).ToHashSet();
            var toUpdatePaths = syncResponse.ToUpdate.Select(f => f.Path).ToHashSet();

            if (syncResponse.PotentialConflicts.Count > 0)
            {
                await ResolveConflictsAsync(ctx, syncResponse.PotentialConflicts, toUpdatePaths, ct);
            }

            logger.LogInformation(
                "Chunk {ChunkNumber}: {ToCreate} to create, {ToUpdate} to update, {Conflicts} conflicts",
                chunkNumber, syncResponse.ToCreate.Count, syncResponse.ToUpdate.Count,
                syncResponse.PotentialConflicts.Count);

            foreach (var fileToCreate in syncResponse.ToCreate)
            {
                if (ct.IsCancellationRequested) break;

                var result = await syncActions.ActionCreateRemoteAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, fileToCreate, ct);

                if (result.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }

                ctx.UploadedPaths.Add(fileToCreate.Path);
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, 0,
                    fileToCreate.Path, result.Action == "Error" ? result.ErrorMessage : null));
            }

            foreach (var fileToUpdate in syncResponse.ToUpdate)
            {
                if (ct.IsCancellationRequested) break;

                if (!toUpdatePaths.Contains(fileToUpdate.Path))
                {
                    continue;
                }

                var result = await syncActions.ActionUpdateRemoteAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, fileToUpdate, ct);

                if (result.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }

                ctx.UploadedPaths.Add(fileToUpdate.Path);
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, 0,
                    fileToUpdate.Path, result.Action == "Error" ? result.ErrorMessage : null));
            }
        }
    }

    public async Task ResolveConflictsAsync(
        SyncContext ctx,
        List<PotentialConflictItem> potentialConflicts,
        HashSet<string> toUpdatePaths,
        CancellationToken ct = default)
    {
        if (potentialConflicts.Count == 0)
        {
            return;
        }

        var result = await syncActions.ActionConflictAsync(
            ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, potentialConflicts, ctx.Options.DryRun, ct);

        ctx.Result = ctx.Result.AddDelta(result.Counts ?? SyncActionCounts.Empty);

        foreach (var toUploadPath in result.ToUpdatePaths)
        {
            toUpdatePaths.Add(toUploadPath);
        }

        foreach (var conflict in potentialConflicts)
        {
            if (!result.ToUpdatePaths.Contains(conflict.Path) && conflict.SongId.HasValue)
            {
                ctx.ConflictedSongIds.Add(conflict.SongId.Value);
            }
        }
    }

    public async Task ServerActionsPhaseAsync(
        SyncContext ctx,
        IProgress<SyncProgress>? progress,
        CancellationToken ct = default)
    {
        if (ctx.Options.Direction == SyncDirection.Up)
        {
            logger.LogInformation("Skipping server actions phase (direction: up)");
            return;
        }

        List<SyncRecordItem> recordsToProcess;

        logger.LogInformation("Fetching pending actions for device {DeviceId}", ctx.DeviceId);
        var pendingResponse = await apiClient.CreatePendingActionsAsync(ctx.DeviceId, ctx.SessionId, ct);

        var existingIds = ctx.PendingServerRecords.Select(r => r.Id).ToHashSet();
        var newRecords = pendingResponse.Records
            .Where(r => !existingIds.Contains(r.Id))
            .ToList();
        ctx.PendingServerRecords.AddRange(newRecords);

        recordsToProcess = ctx.PendingServerRecords;

        if (ctx.Options.Direction == SyncDirection.Down)
        {
            recordsToProcess = recordsToProcess
                .Where(r => r.Action == SyncRecordAction.CreateLocal || r.Action == SyncRecordAction.UpdateLocal)
                .ToList();
            logger.LogInformation("Found {Count} songs to download", recordsToProcess.Count);
        }
        else
        {
            logger.LogInformation("Processing {Count} pending actions", recordsToProcess.Count);
        }

        var serverTotal = recordsToProcess.Count;

        foreach (var record in recordsToProcess)
        {
            if (ct.IsCancellationRequested) break;

            var fullPath = Path.Combine(ctx.RepositoryPath, record.FilePath);

            if (ctx.UploadedPaths.Contains(record.FilePath) && record.Action != SyncRecordAction.CreateLocal && record.Action != SyncRecordAction.UpdateLocal)
            {
                await apiClient.AcknowledgeActionAsync(ctx.DeviceId, ctx.SessionId, new AcknowledgeActionRequest
                {
                    RecordIds = [record.Id]
                }, ct);
                continue;
            }

            if ((record.Action == SyncRecordAction.CreateLocal || record.Action == SyncRecordAction.UpdateLocal) && record.SongId.HasValue && ctx.ConflictedSongIds.Contains(record.SongId.Value))
            {
                logger.LogInformation("Skipping download for song {SongId} - unresolved conflict", record.SongId);
                continue;
            }

            if (record.Action == SyncRecordAction.CreateLocal)
            {
                logger.LogInformation("Creating local song {SongId} at {Path}", record.SongId, record.FilePath);

                var result = await syncActions.ActionCreateLocalAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.SongId, record.FilePath,
                    ctx.Options.DryRun, ctx.Options.AutoConfirm,
                    record.Id, record.Reason, ct);

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.UpdateLocal)
            {
                logger.LogInformation("Updating local song {SongId} at {Path}", record.SongId, record.FilePath);

                var result = await syncActions.ActionUpdateLocalAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.SongId, record.FilePath,
                    ctx.Options.DryRun, ctx.Options.AutoConfirm,
                    record.Id, record.Reason, ct);

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.Unlink || record.Action == SyncRecordAction.Delete)
            {
                var result = await syncActions.ActionDeleteAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.SongId, record.FilePath,
                    ctx.Options.DryRun, ctx.Options.AutoConfirm, record.Id, record.Reason, ct);

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.Rename)
            {
                var renameData = DeserializeRenameData(record.Data);
                if (renameData != null)
                {
                    var result = await syncActions.ActionRenameAsync(
                        ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.FilePath, renameData.PreviousPath,
                        ctx.Options.DryRun, record.Id, ct);

                    if (result?.Counts != null)
                    {
                        ctx.Result = ctx.Result.AddDelta(result.Counts);
                    }
                }
            }

            progress?.Report(SyncProgress.FromResult(
                ctx.Result, "server", serverTotal, recordsToProcess.IndexOf(record) + 1, record.FilePath));
        }
    }

    public async Task CommitPhaseAsync(
        SyncContext ctx,
        IProgress<SyncProgress>? progress,
        CancellationToken ct = default)
    {
        var directionString = ctx.Options.Direction switch
        {
            SyncDirection.Up => "up",
            SyncDirection.Down => "down",
            _ => "both"
        };

        logger.LogInformation("Committing sync session {SessionId} (direction: {Direction})", ctx.SessionId, directionString);

        var commitResult = await apiClient.CommitSyncAsync(ctx.DeviceId, ctx.SessionId, new CommitSyncRequest
        {
            Direction = directionString
        }, ct);

        logger.LogInformation(
            "Commit result: {CreateRemote} created remote, {UpdateRemote} updated remote, {Skipped} skipped, {CreateLocal} created local, {UpdateLocal} updated local, {Delete} deleted, {Link} linked, {Unlink} unlinked, {Rename} renamed, {Conflict} conflicts, {UpdateTimestamp} timestamps updated, {Error} error",
            commitResult.CreateRemoteCount, commitResult.UpdateRemoteCount, commitResult.SkippedCount,
            commitResult.CreateLocalCount, commitResult.UpdateLocalCount, commitResult.DeleteCount,
            commitResult.LinkCount, commitResult.UnlinkCount, commitResult.RenameCount,
            commitResult.ConflictCount, commitResult.UpdateTimestampCount,
            commitResult.ErrorCount);

        ctx.Result = ctx.Result with
        {
            CreateRemote = commitResult.CreateRemoteCount,
            UpdateRemote = commitResult.UpdateRemoteCount,
            Skipped = commitResult.SkippedCount,
            CreateLocal = commitResult.CreateLocalCount,
            UpdateLocal = commitResult.UpdateLocalCount,
            Delete = commitResult.DeleteCount,
            Link = commitResult.LinkCount,
            Unlink = commitResult.UnlinkCount,
            Rename = commitResult.RenameCount,
            Conflict = commitResult.ConflictCount,
            UpdateTimestamp = commitResult.UpdateTimestampCount,
            Error = commitResult.ErrorCount
        };

        progress?.Report(SyncProgress.FromResult(
            ctx.Result, "commit", 1, 1));
    }

    public async Task CompleteAsync(
        SyncContext ctx,
        int filesCount,
        CancellationToken ct = default)
    {
        var directionString = ctx.Options.Direction switch
        {
            SyncDirection.Up => "up",
            SyncDirection.Down => "down",
            _ => "both"
        };

        var completeResponse = await apiClient.CompleteSyncAsync(ctx.DeviceId, ctx.SessionId, new CompleteSyncRequest
        {
            Direction = directionString
        }, ct);

        logger.LogInformation(
            "Sync complete: {CreateRemote} created remote, {UpdateRemote} updated remote, {Skipped} skipped, {CreateLocal} created local, {UpdateLocal} updated local, {Delete} deleted, {Link} linked, {Unlink} unlinked, {Rename} renamed, {Conflict} conflicts, {UpdateTimestamp} timestamps updated, {Error} error",
            completeResponse.CreateRemoteCount, completeResponse.UpdateRemoteCount, completeResponse.SkippedCount,
            completeResponse.CreateLocalCount, completeResponse.UpdateLocalCount, completeResponse.DeleteCount,
            completeResponse.LinkCount, completeResponse.UnlinkCount, completeResponse.RenameCount,
            completeResponse.ConflictCount, completeResponse.UpdateTimestampCount,
            completeResponse.ErrorCount);

        ctx.Result = ctx.Result with
        {
            CreateRemote = completeResponse.CreateRemoteCount,
            UpdateRemote = completeResponse.UpdateRemoteCount,
            Skipped = completeResponse.SkippedCount,
            CreateLocal = completeResponse.CreateLocalCount,
            UpdateLocal = completeResponse.UpdateLocalCount,
            Delete = completeResponse.DeleteCount,
            Link = completeResponse.LinkCount,
            Unlink = completeResponse.UnlinkCount,
            Rename = completeResponse.RenameCount,
            Conflict = completeResponse.ConflictCount,
            UpdateTimestamp = completeResponse.UpdateTimestampCount,
            Error = completeResponse.ErrorCount
        };
    }

    private static readonly JsonSerializerOptions RenameDataOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static RenameData? DeserializeRenameData(JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind == JsonValueKind.Null)
            return null;
        try
        {
            return JsonSerializer.Deserialize<RenameData>(data.Value, RenameDataOptions);
        }
        catch
        {
            return null;
        }
    }
}

internal record RenameData
{
    public required string PreviousPath { get; init; }
    public required string NewPath { get; init; }
}