namespace MyMusic.CLI.Services.Sync;

using System.Diagnostics;
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
            Direction = ctx.Options.Direction,
            RepositoryPath = ctx.RepositoryPath,
            Deduplicate = ctx.Options.Deduplicate,
            RecordSkipped = ctx.Options.RecordSkipped,
            ScanErrors = scanErrors,
            // A dry run doesn't save the device options, so the session previews the local ones
            DeviceOptions = ctx.Options.DryRun
                ? new StartSyncDeviceOptions { NamingTemplate = config.GetNamingTemplate() }
                : null
        }, ct);
        ctx.SessionId = startResponse.SessionId;
        logger.LogInformation("Started sync session: {SessionId} (DryRun: {DryRun}, Direction: {Direction}, Deduplicate: {Deduplicate})", ctx.SessionId, ctx.Options.DryRun, ctx.Options.Direction, ctx.Options.Deduplicate);
    }

    /// <summary>
    /// With deduplication, has the server fingerprint its library before the uploads, one batch per request,
    /// so its progress is shown instead of stalling the first upload. Only uploads are deduplicated, so it is
    /// skipped in <c>down</c>.
    /// </summary>
    public async Task PrepareDeduplicatePhaseAsync(
        SyncContext ctx,
        IProgress<SyncProgress>? progress,
        CancellationToken ct = default)
    {
        if (!ctx.Options.Deduplicate || ctx.Options.Direction == SyncDirection.Down)
        {
            return;
        }

        logger.LogInformation("Fingerprinting server songs for deduplication");
        progress?.Report(SyncProgress.ForPhase("fingerprinting", "Fingerprinting server songs..."));

        PrepareDeduplicateResult result;
        do
        {
            ct.ThrowIfCancellationRequested();

            result = await apiClient.PrepareDeduplicateAsync(ctx.DeviceId, ctx.SessionId, ct);
            progress?.Report(SyncProgress.FromResult(ctx.Result, "fingerprinting", result.Total, result.Processed));
        } while (!result.Done);

        logger.LogInformation("Fingerprinted {Count} server songs for deduplication", result.Total);
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

        // Resets the progress left by the previous phase (fingerprinting) before the first check returns
        progress?.Report(SyncProgress.FromResult(ctx.Result, "upload", files.Count, 0));

        var chunkTuning = config.GetChunkTuning();
        var chunkSize = new AdaptiveChunkSize(chunkTuning.Check, chunkTuning);

        var processedCount = 0;
        var chunkNumber = 0;

        while (processedCount < files.Count)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var chunk = files.GetRange(processedCount, Math.Min(chunkSize.Current, files.Count - processedCount));
            chunkNumber++;
            var chunkProcessedCount = 0;
            logger.LogInformation(
                "Processing chunk {ChunkNumber}: {ChunkFiles} files ({CheckedFiles}/{TotalFiles} checked)",
                chunkNumber, chunk.Count, processedCount, files.Count);

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

            // A failed check aborts the sync: its records would be lost, and the commit rejects unacknowledged ones
            var checkStartedAt = Stopwatch.GetTimestamp();
            var syncResponse = await apiClient.CheckSyncAsync(ctx.DeviceId, ctx.SessionId, syncRequest, ct);
            chunkSize.Report(chunk.Count, Stopwatch.GetElapsedTime(checkStartedAt));

            ctx.Result = ctx.Result.AddDelta(syncResponse.Counts);

            if (syncResponse.Records.Count > 0)
            {
                ctx.PendingServerRecords.AddRange(syncResponse.Records);
                logger.LogInformation(
                    "Chunk {ChunkNumber}: {RecordCount} server action records accumulated",
                    chunkNumber, syncResponse.Records.Count);
            }

            var conflictRecords = syncResponse.Records.Where(r => r.Action == SyncRecordAction.Conflict).ToList();
            var updateLocalRecords = syncResponse.Records.Where(r => r.Action == SyncRecordAction.UpdateLocal).ToList();

            if (conflictRecords.Count > 0 || updateLocalRecords.Count > 0)
            {
                await ResolveConflictsAsync(ctx, conflictRecords, updateLocalRecords, resolvedCount =>
                {
                    chunkProcessedCount += resolvedCount;
                    progress?.Report(SyncProgress.FromResult(
                        ctx.Result, "resolving", files.Count, processedCount + chunkProcessedCount));
                }, ct, syncFiles);

                var superseded = conflictRecords
                    .Concat(updateLocalRecords)
                    .ToHashSet(ReferenceEqualityComparer.Instance);
                ctx.PendingServerRecords.RemoveAll(superseded.Contains);
            }

            var toCreateRecords = syncResponse.Records.Where(r => r.Action == SyncRecordAction.CreateRemote).ToList();
            var toUpdatePaths = new HashSet<string>();
            foreach (var updateRecord in syncResponse.Records.Where(r => r.Action == SyncRecordAction.UpdateRemote))
            {
                toUpdatePaths.Add(updateRecord.FilePath);
            }

            logger.LogInformation(
                "Chunk {ChunkNumber}: {ToCreate} to create, {ToUpdate} to update, {Conflicts} conflicts, {PotentialUpdates} potential updates",
                chunkNumber, toCreateRecords.Count, toUpdatePaths.Count,
                conflictRecords.Count, updateLocalRecords.Count);

            foreach (var createRecord in toCreateRecords)
            {
                if (ct.IsCancellationRequested) break;

                var createData = createRecord.Data.HasValue ? DeserializeCheckCreateUpdateData(createRecord.Data) : null;
                var fileInfo = new SyncFileInfo
                {
                    Path = createRecord.FilePath,
                    ModifiedAt = createData?.ModifiedAt ?? DateTime.MinValue,
                    CreatedAt = createData?.CreatedAt ?? DateTime.MinValue,
                    Reason = createRecord.Reason
                };

                var result = await syncActions.ActionCreateRemoteAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, fileInfo, ct);

                if (result.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }

                AddUploadClientActions(ctx, result);

                ctx.UploadedPaths.Add(createRecord.FilePath);
                chunkProcessedCount++;
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, processedCount + chunkProcessedCount,
                    createRecord.FilePath, result.Action == "Error" ? result.ErrorMessage : null));
            }

            foreach (var updateRecord in syncResponse.Records.Where(r => r.Action == SyncRecordAction.UpdateRemote))
            {
                if (ct.IsCancellationRequested) break;

                if (!toUpdatePaths.Contains(updateRecord.FilePath))
                {
                    continue;
                }

                var updateData = updateRecord.Data.HasValue ? DeserializeCheckCreateUpdateData(updateRecord.Data) : null;
                var fileInfo = new SyncFileInfo
                {
                    Path = updateRecord.FilePath,
                    ModifiedAt = updateData?.ModifiedAt ?? DateTime.MinValue,
                    CreatedAt = updateData?.CreatedAt ?? DateTime.MinValue,
                    Reason = updateRecord.Reason
                };

                var result = await syncActions.ActionUpdateRemoteAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, fileInfo, ct);

                if (result.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }

                AddUploadClientActions(ctx, result);

                ctx.UploadedPaths.Add(updateRecord.FilePath);
                chunkProcessedCount++;
                progress?.Report(SyncProgress.FromResult(
                    ctx.Result, "upload", files.Count, processedCount + chunkProcessedCount,
                    updateRecord.FilePath, result.Action == "Error" ? result.ErrorMessage : null));
            }

            processedCount += chunk.Count;
            progress?.Report(SyncProgress.FromResult(
                ctx.Result, "upload", files.Count, processedCount));
        }
    }

    /// <summary>
    /// Queues the device actions the server recorded for an uploaded file (an <c>UpdateLocal</c> when the
    /// file is a previous version of a song), so the server actions phase performs them in this session.
    /// </summary>
    private static void AddUploadClientActions(SyncContext ctx, SyncActionsDevice.ActionResult result)
    {
        foreach (var record in result.Records ?? [])
        {
            if (record.Action == SyncRecordAction.UpdateLocal ||
                record.Action == SyncRecordAction.Rename)
            {
                ctx.PendingServerRecords.Add(record);
            }
        }
    }

    public async Task ResolveConflictsAsync(
        SyncContext ctx,
        List<SyncRecordItem> conflictRecords,
        List<SyncRecordItem> updateLocalRecords,
        Action<int>? onFilesResolved = null,
        CancellationToken ct = default,
        IReadOnlyList<SyncFileInfo>? files = null)
    {
        if (conflictRecords.Count > 0 || updateLocalRecords.Count > 0)
        {
            var result = await syncActions.ActionConflictAsync(
                ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, conflictRecords, updateLocalRecords, onFilesResolved, ct,
                ctx.Options, files);

            ctx.Result = ctx.Result.AddDelta(result.Counts);

            TrackConflictedPaths(ctx, conflictRecords, result.Records);

            foreach (var uploadedPath in result.UploadedPaths ?? [])
            {
                ctx.UploadedPaths.Add(uploadedPath);
            }

            foreach (var record in result.Records)
            {
                if (record.Action == SyncRecordAction.UpdateLocal ||
                    record.Action == SyncRecordAction.Rename)
                {
                    ctx.PendingServerRecords.Add(record);
                }
            }
        }
    }

    /// <summary>
    /// Marks the paths of conflicts, so the server actions phase does not download over (or rename) their
    /// local files. Other paths of the same song sync normally. The set only grows during a session: a path
    /// is unmarked only when the resolve result settles it (<c>UpdateTimestamp</c>, <c>UpdateLocal</c> or
    /// <c>Skipped</c>), or when the user resolved its conflict (a record pointing to the conflict). If the
    /// resolve request fails, its paths stay marked.
    /// </summary>
    private static void TrackConflictedPaths(
        SyncContext ctx,
        List<SyncRecordItem> conflictRecords,
        List<SyncRecordItem> resolvedRecords)
    {
        foreach (var record in conflictRecords.Concat(resolvedRecords))
        {
            if (record.Action is SyncRecordAction.Conflict or SyncRecordAction.Error)
            {
                ctx.ConflictedPaths.Add(record.FilePath);
            }
        }

        foreach (var record in resolvedRecords)
        {
            if (record.Action is SyncRecordAction.UpdateTimestamp or SyncRecordAction.UpdateLocal or SyncRecordAction.Skipped)
            {
                ctx.ConflictedPaths.Remove(record.FilePath);
            }

            if (record.ResolvesConflictRecordId.HasValue)
            {
                var conflict = resolvedRecords.FirstOrDefault(r => r.Id == record.ResolvesConflictRecordId.Value);
                if (conflict != null)
                {
                    ctx.ConflictedPaths.Remove(conflict.FilePath);
                }
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

        logger.LogInformation("Fetching pending actions for device {DeviceId}", ctx.DeviceId);
        var pendingResponse = await apiClient.CreatePendingActionsAsync(ctx.DeviceId, ctx.SessionId, ct);
        ctx.Result = ctx.Result.AddDelta(pendingResponse.Counts);

        var existingIds = ctx.PendingServerRecords.Select(r => r.Id).ToHashSet();
        var newRecords = pendingResponse.Records
            .Where(r => !existingIds.Contains(r.Id))
            .ToList();
        ctx.PendingServerRecords.AddRange(newRecords);

        // Every client-action record must be processed (and acknowledged), regardless of direction,
        // otherwise the commit rejects the session. In `down` the server is the source of truth, so
        // its deletions and renames are applied just like downloads.
        // The deletions go first: the server gives the path of a file it deletes to another file of the same
        // session, whose record may come before the DeleteLocal.
        var recordsToProcess = ctx.PendingServerRecords
            .OrderBy(r => r.Action != SyncRecordAction.DeleteLocal)
            .ToList();
        logger.LogInformation("Processing {Count} pending actions", recordsToProcess.Count);

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

            if (record.Action == SyncRecordAction.UpdateLocal && ctx.ConflictedPaths.Contains(record.FilePath))
            {
                logger.LogInformation("Skipping download for song {SongId} at {Path} - unresolved conflict", record.SongId, record.FilePath);

                var result = await syncActions.ReportFailureAsync(
                    ctx.DeviceId, ctx.SessionId, record.Id, record.FilePath, record.SongId,
                    "Unresolved conflict", record.Reason ?? "Server-initiated update", ct);

                if (result.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.CreateLocal)
            {
                logger.LogInformation("Creating local song {SongId} at {Path}", record.SongId, record.FilePath);

                var result = await syncActions.ActionCreateLocalAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.SongId, record.FilePath,
                    ctx.Options.DryRun, ctx.Options.AutoConfirm,
                    record.Id, record.Reason, GetFileModifiedAt(record.Data, config.GetFileModifiedAt()), ct);

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
                    record.Id, record.Reason, GetLocalSourcePath(record.Data),
                    GetFileModifiedAt(record.Data, config.GetFileModifiedAt()), ct);

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.DeleteLocal)
            {
                var result = await syncActions.ActionDeleteLocalAsync(
                    ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.SongId, record.FilePath,
                    ctx.Options.DryRun, ctx.Options.AutoConfirm, record.Id, record.Reason, ct);

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.Unlink)
            {
                var result = await syncActions.ActionUnlinkAsync(
                    ctx.DeviceId, ctx.SessionId, record.SongId, record.FilePath,
                    ctx.Options.DryRun, record.Id, record.Reason, ct);

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
                }
            }
            else if (record.Action == SyncRecordAction.Rename)
            {
                var renameData = DeserializeRenameData(record.Data);
                SyncActionsDevice.ActionResult? result;
                if (renameData == null)
                {
                    logger.LogWarning("Skipping rename of record {RecordId} to {Path} - missing rename data", record.Id, record.FilePath);
                    result = await syncActions.ReportFailureAsync(
                        ctx.DeviceId, ctx.SessionId, record.Id, record.FilePath, record.SongId,
                        "Missing rename data", "Server-initiated rename", ct);
                }
                else if (ctx.ConflictedPaths.Contains(renameData.PreviousPath))
                {
                    logger.LogInformation("Skipping rename of {PreviousPath} to {Path} - unresolved conflict", renameData.PreviousPath, record.FilePath);
                    result = await syncActions.ReportFailureAsync(
                        ctx.DeviceId, ctx.SessionId, record.Id, record.FilePath, record.SongId,
                        "Unresolved conflict", $"Rename from '{renameData.PreviousPath}'", ct);
                }
                else
                {
                    result = await syncActions.ActionRenameAsync(
                        ctx.DeviceId, ctx.SessionId, ctx.RepositoryPath, record.FilePath, renameData.PreviousPath,
                        ctx.Options.DryRun, record.Id, ct);
                }

                if (result?.Counts != null)
                {
                    ctx.Result = ctx.Result.AddDelta(result.Counts);
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
        logger.LogInformation("Committing sync session {SessionId} (direction: {Direction})", ctx.SessionId, ctx.Options.Direction);

        var commitResult = await apiClient.CommitSyncAsync(ctx.DeviceId, ctx.SessionId, ct);

        logger.LogInformation(
            "Commit result: {CreateRemote} created remote, {UpdateRemote} updated remote, {Skipped} skipped, {CreateLocal} created local, {UpdateLocal} updated local, {DeleteLocal} deleted local, {Link} linked, {Unlink} unlinked, {Rename} renamed, {Conflict} conflicts, {UpdateTimestamp} timestamps updated, {Error} error",
            commitResult.CreateRemoteCount, commitResult.UpdateRemoteCount, commitResult.SkippedCount,
            commitResult.CreateLocalCount, commitResult.UpdateLocalCount, commitResult.DeleteLocalCount,
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
            DeleteLocal = commitResult.DeleteLocalCount,
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
        var completeResponse = await apiClient.CompleteSyncAsync(ctx.DeviceId, ctx.SessionId, ct);

        logger.LogInformation(
            "Sync complete: {CreateRemote} created remote, {UpdateRemote} updated remote, {Skipped} skipped, {CreateLocal} created local, {UpdateLocal} updated local, {DeleteLocal} deleted local, {Link} linked, {Unlink} unlinked, {Rename} renamed, {Conflict} conflicts, {UpdateTimestamp} timestamps updated, {Error} error",
            completeResponse.CreateRemoteCount, completeResponse.UpdateRemoteCount, completeResponse.SkippedCount,
            completeResponse.CreateLocalCount, completeResponse.UpdateLocalCount, completeResponse.DeleteLocalCount,
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
            DeleteLocal = completeResponse.DeleteLocalCount,
            Link = completeResponse.LinkCount,
            Unlink = completeResponse.UnlinkCount,
            Rename = completeResponse.RenameCount,
            Conflict = completeResponse.ConflictCount,
            UpdateTimestamp = completeResponse.UpdateTimestampCount,
            Error = completeResponse.ErrorCount
        };
    }

    internal static SyncCheckCreateUpdateData? DeserializeCheckCreateUpdateData(System.Text.Json.JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
            return null;
        try
        {
            return data.Value.Deserialize(SyncDataJsonContext.Default.SyncCheckCreateUpdateData);
        }
        catch
        {
            return null;
        }
    }

    internal record SyncCheckCreateUpdateData
    {
        public required DateTime ModifiedAt { get; init; }
        public required DateTime CreatedAt { get; init; }
        public string? Reason { get; init; }
    }

    /// <summary>
    /// The device path an <c>UpdateLocal</c> copies its content from, instead of downloading it (soundalikes of
    /// files uploaded in the same session).
    /// </summary>
    private static string? GetLocalSourcePath(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty("localSourcePath", out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// The modified date a downloaded file gets, out of the song's dates in its record. Null leaves the file
    /// with the time it was synced at: the device is set to it, or the server did not send the date.
    /// </summary>
    internal static DateTime? GetFileModifiedAt(JsonElement? data, FileModifiedAtSource source)
    {
        var property = source switch
        {
            FileModifiedAtSource.ServerModifiedAt => "serverModifiedAt",
            FileModifiedAtSource.ServerCreatedAt => "serverCreatedAt",
            _ => null,
        };

        return property != null
               && data is { ValueKind: JsonValueKind.Object } element
               && element.TryGetProperty(property, out var value)
               && value.ValueKind == JsonValueKind.String
               && value.TryGetDateTime(out var date)
            ? date.ToUniversalTime()
            : null;
    }

    internal static RenameData? DeserializeRenameData(JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind == JsonValueKind.Null)
            return null;
        try
        {
            return data.Value.Deserialize(SyncDataJsonContext.Default.RenameData);
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