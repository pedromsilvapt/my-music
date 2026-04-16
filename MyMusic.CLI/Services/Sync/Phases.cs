namespace MyMusic.CLI.Services.Sync;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Services.Sync.Types;

public class Phases(
    ISyncApiClient apiClient,
    IFileOps fileOps,
    IUserPrompt userPrompt,
    IFileSystem fileSystem,
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
            ctx.Result = ctx.Result with { Failed = ctx.Result.Failed + scanErrors.Count };
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
            RepositoryPath = ctx.RepositoryPath
        }, ct);
        ctx.SessionId = startResponse.SessionId;
        logger.LogInformation("Started sync session: {SessionId} (DryRun: {DryRun})", ctx.SessionId, ctx.Options.DryRun);

        if (scanErrors.Count > 0)
        {
            var errorRecords = scanErrors.Select(e => new RecordItem
            {
                FilePath = e.Path,
                Action = "Error",
                Source = "Device",
                ErrorMessage = e.Error
            }).ToList();

            await apiClient.RecordChunkAsync(ctx.DeviceId, ctx.SessionId, new RecordChunkRequest
            {
                Records = errorRecords
            }, ct);

            logger.LogInformation("Recorded {Count} scan errors to session", scanErrors.Count);
        }
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

        var pendingResponse = await apiClient.GetPendingActionsAsync(ctx.DeviceId, ct);
        ctx.PendingActions = pendingResponse.Actions;
        ctx.PendingDownloadPaths = pendingResponse.Actions
            .Where(a => a.Action == "Download")
            .Select(a => a.Path)
            .ToHashSet();
        logger.LogInformation("Found {Count} pending actions", ctx.PendingActions.Count);

        if (files.Count == 0)
        {
            return;
        }

        var chunkSize = config.GetChunkSize();
        var chunks = files.Chunk(chunkSize).ToList();
        logger.LogInformation("Processing {ChunkCount} chunks", chunks.Count);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        for (var i = 0; i < chunks.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var chunk = chunks[i];
            var chunkNumber = i + 1;
            logger.LogInformation("Processing chunk {ChunkNumber}/{TotalChunks}", chunkNumber, chunks.Count);

            var syncFiles = chunk.Select(f => new SyncFileInfo
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
                syncResponse = await apiClient.CheckSyncAsync(ctx.DeviceId, syncRequest, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check sync for chunk {ChunkNumber}", chunkNumber);
                failed += chunk.Length;
                ctx.Result = ctx.Result with { Failed = failed };
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, (i + 1) * chunkSize,
                    errorMessage: $"Chunk {chunkNumber} failed: {ex.Message}"));
                continue;
            }

            if (syncResponse.PendingActions.Count > 0)
            {
                MergePendingActions(ctx, syncResponse.PendingActions);
            }

            var toCreatePaths = syncResponse.ToCreate.Select(f => f.Path).ToHashSet();
            var toUpdatePaths = syncResponse.ToUpdate.Select(f => f.Path).ToHashSet();
            var recordItems = new List<RecordItem>();

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

                var record = await UploadOneFileAsync(ctx, fileToCreate, "Created", ct);
                if (record.Action == "Created")
                {
                    created++;
                }
                else if (record.Action == "Error")
                {
                    failed++;
                }
                recordItems.Add(record);
                ctx.UploadedPaths.Add(fileToCreate.Path);
                ctx.Result = ctx.Result with { Created = created, Updated = updated, Skipped = skipped, Failed = failed };
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, created + updated + skipped + failed,
                    fileToCreate.Path, record.Action == "Error" ? record.ErrorMessage : null));
            }

            foreach (var fileToUpdate in syncResponse.ToUpdate)
            {
                if (ct.IsCancellationRequested) break;

                if (!toUpdatePaths.Contains(fileToUpdate.Path))
                {
                    continue;
                }

                var record = await UploadOneFileAsync(ctx, fileToUpdate, "Updated", ct);
                if (record.Action == "Updated")
                {
                    updated++;
                }
                else if (record.Action == "Error")
                {
                    failed++;
                }
                recordItems.Add(record);
                ctx.UploadedPaths.Add(fileToUpdate.Path);
                ctx.Result = ctx.Result with { Created = created, Updated = updated, Skipped = skipped, Failed = failed };
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, created + updated + skipped + failed,
                    fileToUpdate.Path, record.Action == "Error" ? record.ErrorMessage : null));
            }

            var conflictPaths = syncResponse.PotentialConflicts.Select(c => c.Path).ToHashSet();

            foreach (var file in chunk)
            {
                var inCreate = toCreatePaths.Contains(file.RelativePath);
                var inUpdate = toUpdatePaths.Contains(file.RelativePath);
                var isPendingDownload = ctx.PendingDownloadPaths.Contains(file.RelativePath);
                var isConflict = conflictPaths.Contains(file.RelativePath);

                if (!inCreate && !inUpdate && !isPendingDownload && !isConflict)
                {
                    skipped++;
                    recordItems.Add(new RecordItem
                    {
                        FilePath = file.RelativePath,
                        Action = "Skipped",
                        Source = "Device",
                        Reason = $"File unchanged (modified at {file.ModifiedAt:O})"
                    });
                }
            }

            await apiClient.RecordChunkAsync(ctx.DeviceId, ctx.SessionId, new RecordChunkRequest
            {
                Records = recordItems
            }, ct);
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

        if (ctx.Options.DryRun)
        {
            logger.LogInformation("Dry-run: {Count} potential conflicts detected (not resolved)", potentialConflicts.Count);
            ctx.Result = ctx.Result with { Conflicts = ctx.Result.Conflicts + potentialConflicts.Count };
            return;
        }

        logger.LogInformation("Found {Count} potential conflicts, reading file content", potentialConflicts.Count);

        var resolveItems = new List<ConflictResolveItem>();
        foreach (var conflict in potentialConflicts)
        {
            var fullPath = Path.Combine(ctx.RepositoryPath, conflict.Path);
            if (!fileSystem.File.Exists(fullPath))
            {
                logger.LogWarning("Conflict file not found locally: {Path}", conflict.Path);
                continue;
            }

            var fileBytes = fileSystem.File.ReadAllBytes(fullPath);
            var fileContentBase64 = Convert.ToBase64String(fileBytes);
            resolveItems.Add(new ConflictResolveItem
            {
                Path = conflict.Path,
                SongId = conflict.SongId,
                FileContentBase64 = fileContentBase64,
                LocalModifiedAt = conflict.LocalModifiedAt.ToString("O")
            });
        }

        if (resolveItems.Count == 0)
        {
            return;
        }

        try
        {
            var resolveResponse = await apiClient.ResolveConflictsAsync(ctx.DeviceId, new ResolveConflictsRequest
            {
                Conflicts = resolveItems
            }, ct);

            foreach (var resolved in resolveResponse.Resolved)
            {
                logger.LogInformation("Resolved conflict for {Path}: {Reason}", resolved.Path, resolved.Reason);
            }

            foreach (var toUploadItem in resolveResponse.ToUpload)
            {
                toUpdatePaths.Add(toUploadItem.Path);
            }

            foreach (var conflictError in resolveResponse.Conflicts)
            {
                logger.LogError("Conflict for {Path}: {Reason}", conflictError.Path, conflictError.Reason);
                ctx.Result = ctx.Result with { Conflicts = ctx.Result.Conflicts + 1 };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve conflicts");
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

        List<PendingActionItem> actionsToProcess;

        if (ctx.Options.Direction == SyncDirection.Down)
        {
            logger.LogInformation("Direction is 'down' - fetching all device songs");
            var allDeviceSongs = await apiClient.GetPendingActionsAsync(ctx.DeviceId, ct);
            actionsToProcess = allDeviceSongs.Actions
                .Where(s => s.Action == "Download" || s.Action == null)
                .ToList();
            logger.LogInformation("Found {Count} songs to download", actionsToProcess.Count);
        }
        else
        {
            actionsToProcess = ctx.PendingActions;
            logger.LogInformation("Processing {Count} pending actions", actionsToProcess.Count);
        }

        var serverTotal = actionsToProcess.Count;
        var downloaded = ctx.Result.Downloaded;
        var removed = ctx.Result.Removed;
        var failed = ctx.Result.Failed;
        var serverRecordItems = new List<RecordItem>();

        foreach (var action in actionsToProcess)
        {
            if (ct.IsCancellationRequested) break;

            var fullPath = Path.Combine(ctx.RepositoryPath, action.Path);

            if (ctx.UploadedPaths.Contains(action.Path) && action.Action != "Download")
            {
                if (!ctx.Options.DryRun)
                {
                    await apiClient.AcknowledgeActionAsync(ctx.DeviceId, new AcknowledgeActionRequest
                    {
                        DevicePath = action.Path
                    }, ct);
                }
                continue;
            }

            if (action.Action == "Download")
            {
                var record = await DownloadOneFileAsync(ctx, action.SongId, fullPath, action.Path, ct);
                if (record != null)
                {
                    if (record.Action == "Downloaded")
                    {
                        downloaded++;
                    }
                    else if (record.Action == "Error")
                    {
                        failed++;
                    }
                    serverRecordItems.Add(record);
                }
            }
            else if (action.Action == "Remove")
            {
                var record = await RemoveOneFileAsync(ctx, action.SongId, fullPath, action.Path, ct);
                if (record != null)
                {
                    if (record.Action == "Removed")
                    {
                        removed++;
                    }
                    else if (record.Action == "Error")
                    {
                        failed++;
                    }
                    serverRecordItems.Add(record);
                }
            }

            ctx.Result = ctx.Result with { Downloaded = downloaded, Removed = removed, Failed = failed };
            progress?.Report(SyncProgress.FromResult(
                ctx.Result, "server", serverTotal, actionsToProcess.IndexOf(action) + 1, action.Path));
        }

        if (serverRecordItems.Count > 0)
        {
            await apiClient.RecordChunkAsync(ctx.DeviceId, ctx.SessionId, new RecordChunkRequest
            {
                Records = serverRecordItems
            }, ct);
        }
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
            "Sync complete: {Created} created, {Updated} updated, {Skipped} skipped, {Downloaded} downloaded, {Removed} removed, {Error} error",
            completeResponse.CreatedCount, completeResponse.UpdatedCount, completeResponse.SkippedCount,
            completeResponse.DownloadedCount, completeResponse.RemovedCount,
            completeResponse.ErrorCount);
    }

    private async Task<RecordItem> UploadOneFileAsync(
        SyncContext ctx,
        SyncFileInfo fileInfo,
        string action,
        CancellationToken ct = default)
    {
        var fullPath = Path.Combine(ctx.RepositoryPath, fileInfo.Path);
        if (!fileSystem.File.Exists(fullPath))
        {
            logger.LogWarning("File not found: {Path}", fullPath);
            return new RecordItem
            {
                FilePath = fileInfo.Path,
                Action = "Error",
                Source = "Device",
                Reason = "File not found"
            };
        }

        if (ctx.Options.DryRun)
        {
            return new RecordItem
            {
                FilePath = fileInfo.Path,
                Action = action,
                Source = "Device",
                Reason = fileInfo.Reason
            };
        }

        try
        {
            await using var stream = fileSystem.File.OpenRead(fullPath);
            var fileName = Path.GetFileName(fullPath);
            await apiClient.UploadFileAsync(ctx.DeviceId, new UploadFileRequest
            {
                FileStream = stream,
                FileName = fileName,
                Path = fileInfo.Path,
                ModifiedAt = fileInfo.ModifiedAt.ToString("O"),
                CreatedAt = fileInfo.CreatedAt.ToString("O")
            }, ct);

            return new RecordItem
            {
                FilePath = fileInfo.Path,
                Action = action,
                Source = "Device",
                Reason = fileInfo.Reason
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Action} file: {Path}", action.ToLower(), fileInfo.Path);
            return new RecordItem
            {
                FilePath = fileInfo.Path,
                Action = "Error",
                ErrorMessage = ex.Message,
                Source = "Device",
                Reason = fileInfo.Reason
            };
        }
    }

    private async Task<RecordItem?> DownloadOneFileAsync(
        SyncContext ctx,
        long? songId,
        string fullPath,
        string relativePath,
        CancellationToken ct = default)
    {
        var fileExists = fileOps.FileExists(fullPath);

        if (!ctx.Options.DryRun && fileExists && !ctx.Options.AutoConfirm)
        {
            var confirmed = await userPrompt.ConfirmDeletionAsync(relativePath, ct);
            if (!confirmed)
            {
                return null;
            }
        }

        if (ctx.Options.DryRun)
        {
            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Downloaded",
                SongId = songId,
                Source = "Server",
                Reason = "Server-initiated download"
            };
        }

        var tempPath = fullPath + ".tmp";
        try
        {
            await fileOps.EnsureDirectoryAsync(fullPath, ct);

            await using var stream = await apiClient.DownloadSongAsync(songId!.Value, ct);
            await fileOps.WriteFileAsync(tempPath, stream, ct);

            if (fileExists)
            {
                await fileOps.DeleteFileAsync(fullPath, ct);
            }

            fileSystem.File.Move(tempPath, fullPath);

            var fileInfo = fileSystem.FileInfo.New(fullPath);
            await apiClient.AcknowledgeActionAsync(ctx.DeviceId, new AcknowledgeActionRequest
            {
                DevicePath = relativePath,
                ModifiedAt = fileInfo.LastWriteTimeUtc
            }, ct);

            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Downloaded",
                SongId = songId,
                Source = "Server",
                Reason = "Server-initiated download"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download file: {Path}", relativePath);
            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Error",
                SongId = songId,
                ErrorMessage = ex.Message,
                Source = "Server",
                Reason = "Server-initiated download failed"
            };
        }
        finally
        {
            if (fileSystem.File.Exists(tempPath))
            {
                fileSystem.File.Delete(tempPath);
            }
        }
    }

    private async Task<RecordItem?> RemoveOneFileAsync(
        SyncContext ctx,
        long? songId,
        string fullPath,
        string relativePath,
        CancellationToken ct = default)
    {
        var fileExists = fileOps.FileExists(fullPath);

        if (!fileExists)
        {
            if (!ctx.Options.DryRun)
            {
                await apiClient.AcknowledgeActionAsync(ctx.DeviceId, new AcknowledgeActionRequest
                {
                    DevicePath = relativePath
                }, ct);
            }
            return null;
        }

        if (!ctx.Options.DryRun && !ctx.Options.AutoConfirm)
        {
            var confirmed = await userPrompt.ConfirmDeletionAsync(relativePath, ct);
            if (!confirmed)
            {
                return null;
            }
        }

        if (ctx.Options.DryRun)
        {
            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Removed",
                SongId = songId,
                Source = "Server",
                Reason = "Server-initiated removal"
            };
        }

        try
        {
            await fileOps.DeleteFileAsync(fullPath, ct);
            await apiClient.AcknowledgeActionAsync(ctx.DeviceId, new AcknowledgeActionRequest
            {
                DevicePath = relativePath
            }, ct);

            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Removed",
                SongId = songId,
                Source = "Server",
                Reason = "Server-initiated removal"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete file: {Path}", relativePath);
            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Error",
                SongId = songId,
                ErrorMessage = ex.Message,
                Source = "Server",
                Reason = "Server-initiated removal failed"
            };
        }
    }

    private void MergePendingActions(SyncContext ctx, List<PendingActionItem> newActions)
    {
        var existingPaths = ctx.PendingActions.Select(a => a.Path).ToHashSet();
        foreach (var newAction in newActions)
        {
            if (existingPaths.Contains(newAction.Path))
            {
                var existingIdx = ctx.PendingActions.FindIndex(a => a.Path == newAction.Path);
                ctx.PendingActions[existingIdx] = newAction;
            }
            else
            {
                ctx.PendingActions.Add(newAction);
            }
        }

        ctx.PendingDownloadPaths = ctx.PendingActions
            .Where(a => a.Action == "Download")
            .Select(a => a.Path)
            .ToHashSet();
    }
}
