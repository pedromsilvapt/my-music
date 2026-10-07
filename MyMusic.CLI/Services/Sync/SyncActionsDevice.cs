namespace MyMusic.CLI.Services.Sync;

using System.Diagnostics;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync.Types;

public class SyncActionsDevice(
    IFileOps fileOps,
    ISyncApiClient apiClient,
    IUserPrompt userPrompt,
    IFileSystem fileSystem,
    ISyncConfig config,
    ILogger<SyncActionsDevice> logger)
{
    // The paths the exclusion rules keep out of the sync: they are not scanned, and no action touches them
    private readonly Lazy<ExclusionMatcher> _exclusions = new(() => new ExclusionMatcher(config.GetExcludePatterns()));

    // The size of the resolve requests, kept for the whole sync so what one request teaches serves the next
    private readonly Lazy<AdaptiveChunkSize> _resolveChunkSize = new(() =>
    {
        var chunkTuning = config.GetChunkTuning();
        return new AdaptiveChunkSize(chunkTuning.Resolve, chunkTuning);
    });

    // The answers the user gave for every remaining question of this sync, so they are not asked again
    private bool? _deletionForAll;
    private ConflictResolution? _conflictForAll;

    public record ActionResult(
        string Action,
        string FilePath,
        string Source = "Device",
        string? Reason = null,
        string? ErrorMessage = null,
        long? SongId = null,
        long? RecordId = null,
        SyncActionCounts? Counts = null,
        List<SyncRecordItem>? Records = null);

    public async Task<ActionResult> ActionCreateRemoteAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        SyncFileInfo fileInfo,
        CancellationToken ct = default)
    {
        var fullPath = Path.Combine(repositoryPath, fileInfo.Path);
        if (!fileSystem.File.Exists(fullPath))
        {
            logger.LogWarning("File not found: {Path}", fullPath);
            return await ReportUploadFailureAsync(deviceId, sessionId, fileInfo, $"File not found: {fullPath}", ct);
        }

        try
        {
            await using var stream = fileSystem.File.OpenRead(fullPath);
            var fileName = Path.GetFileName(fullPath);
            var uploadResult = await apiClient.UploadFileAsync(deviceId, sessionId, new UploadFileRequest
            {
                FileStream = stream,
                FileName = fileName,
                Path = fileInfo.Path,
                ModifiedAt = fileInfo.ModifiedAt.ToUniversalTime().ToString("O"),
                CreatedAt = fileInfo.CreatedAt.ToUniversalTime().ToString("O")
            }, ct);

            return new ActionResult("Created", fileInfo.Path, Reason: fileInfo.Reason, SongId: uploadResult.SongId, Counts: uploadResult.Counts, Records: uploadResult.Records);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload file: {Path}", fileInfo.Path);
            return await ReportUploadFailureAsync(deviceId, sessionId, fileInfo, ex.Message, ct);
        }
    }

    public async Task<ActionResult> ActionUpdateRemoteAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        SyncFileInfo fileInfo,
        CancellationToken ct = default,
        long? resolvesConflictRecordId = null)
    {
        var fullPath = Path.Combine(repositoryPath, fileInfo.Path);
        if (!fileSystem.File.Exists(fullPath))
        {
            logger.LogWarning("File not found: {Path}", fullPath);
            return await ReportUploadFailureAsync(deviceId, sessionId, fileInfo, $"File not found: {fullPath}", ct);
        }

        try
        {
            await using var stream = fileSystem.File.OpenRead(fullPath);
            var fileName = Path.GetFileName(fullPath);
            var uploadResult = await apiClient.UploadFileAsync(deviceId, sessionId, new UploadFileRequest
            {
                FileStream = stream,
                FileName = fileName,
                Path = fileInfo.Path,
                ModifiedAt = fileInfo.ModifiedAt.ToUniversalTime().ToString("O"),
                CreatedAt = fileInfo.CreatedAt.ToUniversalTime().ToString("O"),
                ResolvesConflictRecordId = resolvesConflictRecordId
            }, ct);

            return new ActionResult("Updated", fileInfo.Path, Reason: fileInfo.Reason, SongId: uploadResult.SongId, Counts: uploadResult.Counts, Records: uploadResult.Records);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update file: {Path}", fileInfo.Path);
            return await ReportUploadFailureAsync(deviceId, sessionId, fileInfo, ex.Message, ct);
        }
    }

    public async Task<ActionResult?> ActionCreateLocalAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        long? songId,
        string relativePath,
        bool dryRun,
        bool autoConfirm,
        long recordId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var excluded = await ReportExcludedAsync(deviceId, sessionId, recordId, relativePath, songId, $"{reason ?? "Server-initiated download"} failed", ct: ct);
        if (excluded != null)
        {
            return excluded;
        }

        var fullPath = Path.Combine(repositoryPath, relativePath);
        var fileExists = fileOps.FileExists(fullPath);

        if (fileExists)
        {
            logger.LogError("File already exists during create: {Path}", relativePath);
            return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, "File already exists", "Unexpected local file during create", ct);
        }

        return await DownloadAndAckAsync(deviceId, sessionId, repositoryPath, songId, relativePath, dryRun, recordId, reason, isUpdate: false, ct);
    }

    public async Task<ActionResult?> ActionUpdateLocalAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        long? songId,
        string relativePath,
        bool dryRun,
        bool autoConfirm,
        long recordId,
        string? reason = null,
        string? localSourcePath = null,
        CancellationToken ct = default)
    {
        var excluded = await ReportExcludedAsync(deviceId, sessionId, recordId, relativePath, songId, $"{reason ?? "Server-initiated update"} failed", ct: ct);
        if (excluded != null)
        {
            return excluded;
        }

        var fullPath = Path.Combine(repositoryPath, relativePath);
        var fileExists = fileOps.FileExists(fullPath);

        if (!fileExists)
        {
            logger.LogError("File not found during update: {Path}", relativePath);
            return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, "File not found", "Missing local file during update", ct);
        }

        if (localSourcePath != null && !fileOps.FileExists(Path.Combine(repositoryPath, localSourcePath)))
        {
            logger.LogError("Source file {SourcePath} not found during update of {Path}", localSourcePath, relativePath);
            return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, $"Source file not found: {localSourcePath}", "Missing local source file during update", ct);
        }

        return await DownloadAndAckAsync(deviceId, sessionId, repositoryPath, songId, relativePath, dryRun, recordId, reason, isUpdate: true, ct, localSourcePath);
    }

    private async Task<ActionResult?> DownloadAndAckAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        long? songId,
        string relativePath,
        bool dryRun,
        long recordId,
        string? reason,
        bool isUpdate,
        CancellationToken ct,
        string? localSourcePath = null)
    {
        var actionName = isUpdate ? "UpdateLocal" : "CreateLocal";
        var baseReason = reason ?? (isUpdate ? "Server-initiated update" : "Server-initiated download");
        var fullPath = Path.Combine(repositoryPath, relativePath);

        var tempPath = fullPath + ".tmp";
        try
        {
            // Dry-run skips the download/move, so there is no real file modification time to report.
            DateTime? modifiedAt = null;
            if (!dryRun)
            {
                await fileOps.EnsureDirectoryAsync(fullPath, ct);

                // A soundalike of a file uploaded in this session gets that file's content, which is only
                // on the device until the commit creates its song
                await using var stream = localSourcePath != null
                    ? fileSystem.File.OpenRead(Path.Combine(repositoryPath, localSourcePath))
                    : await apiClient.DownloadSongAsync(songId!.Value, ct);
                await fileOps.WriteFileAsync(tempPath, stream, ct);

                if (isUpdate)
                {
                    await fileOps.DeleteFileAsync(fullPath, ct);
                }

                await fileOps.MoveFileAsync(tempPath, fullPath, ct);

                modifiedAt = await fileOps.GetModificationTimeAsync(fullPath, ct);
            }

            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId],
                ModifiedAt = modifiedAt
            }, ct);

            return new ActionResult(
                actionName,
                relativePath,
                Source: "Server",
                Reason: baseReason,
                SongId: songId,
                RecordId: recordId,
                Counts: ackResult.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Action} file: {Path}", isUpdate ? "update" : "download", relativePath);
            return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, ex.Message, $"{baseReason} failed", ct);
        }
        finally
        {
            if (fileOps.FileExists(tempPath))
            {
                await fileOps.DeleteFileAsync(tempPath, ct);
            }
        }
    }

    public async Task<ActionResult?> ActionDeleteLocalAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        long? songId,
        string relativePath,
        bool dryRun,
        bool autoConfirm,
        long recordId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var excluded = await ReportExcludedAsync(deviceId, sessionId, recordId, relativePath, songId, $"{reason ?? "Server-initiated removal"} failed", ct: ct);
        if (excluded != null)
        {
            return excluded;
        }

        var fullPath = Path.Combine(repositoryPath, relativePath);
        var fileExists = fileOps.FileExists(fullPath);

        if (!fileExists)
        {
            await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
            }, ct);
            return null;
        }

        if (!dryRun && !autoConfirm)
        {
            var confirmed = await ConfirmDeletionAsync(relativePath, ct);
            if (!confirmed)
            {
                logger.LogInformation("Deletion declined by user: {Path}", relativePath);
                return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, "Deletion declined by user", reason ?? "Server-initiated removal", ct);
            }
        }

        var baseReason = reason ?? "Server-initiated removal";

        try
        {
            if (!dryRun)
            {
                await fileOps.DeleteFileAsync(fullPath, ct);
            }

            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
            }, ct);

            return new ActionResult("DeleteLocal", relativePath, Source: "Server", Reason: baseReason, SongId: songId, RecordId: recordId, Counts: ackResult.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete file: {Path}", relativePath);
            return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, ex.Message, $"{baseReason} failed", ct);
        }
    }

    /// <summary>
    /// Acknowledges an Unlink record without touching the local filesystem.
    /// Unlink is emitted by orphan detection when the client's local file is
    /// already gone; the server only needs to sever the SongDevice association.
    /// </summary>
    public async Task<ActionResult?> ActionUnlinkAsync(
        long deviceId,
        long sessionId,
        long? songId,
        string relativePath,
        bool dryRun,
        long recordId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var baseReason = reason ?? "Orphaned: path not present locally";

        var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
        {
            RecordIds = [recordId]
        }, ct);

        return new ActionResult("Unlink", relativePath, Source: "Server", Reason: baseReason, SongId: songId, RecordId: recordId, Counts: ackResult.Counts);
    }

    public async Task<ActionResult?> ActionRenameAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        string relativePath,
        string previousRelativePath,
        bool dryRun,
        long recordId,
        CancellationToken ct = default)
    {
        var excluded = await ReportExcludedAsync(deviceId, sessionId, recordId, relativePath, songId: null, $"Rename from '{previousRelativePath}' failed", previousRelativePath, ct);
        if (excluded != null)
        {
            return excluded;
        }

        var fullPath = Path.Combine(repositoryPath, relativePath);
        var previousFullPath = Path.Combine(repositoryPath, previousRelativePath);

        try
        {
            if (!dryRun && fileOps.FileExists(previousFullPath))
            {
                await fileOps.EnsureDirectoryAsync(fullPath, ct);
                await fileOps.MoveFileAsync(previousFullPath, fullPath, ct);
                fileOps.CleanupEmptyParentDirectories(previousFullPath, repositoryPath);
            }

            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
            }, ct);

            return new ActionResult("Renamed", relativePath, Source: "Server", Reason: $"Renamed from '{previousRelativePath}'", RecordId: recordId, Counts: ackResult.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename file: {PreviousPath} -> {Path}", previousRelativePath, relativePath);
            return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId: null, ex.Message, $"Rename from '{previousRelativePath}' failed", ct);
        }
    }

    /// <summary>
    /// Reports an action on a path that an exclusion rule matches as an <c>Error</c>, whether or not the file
    /// exists and in a dry run as well: a sync never creates, changes, moves or deletes an excluded file.
    /// Returns null when neither the path nor <paramref name="otherPath"/> (the path a rename moves from) is excluded.
    /// </summary>
    private async Task<ActionResult?> ReportExcludedAsync(
        long deviceId,
        long sessionId,
        long recordId,
        string relativePath,
        long? songId,
        string reason,
        string? otherPath = null,
        CancellationToken ct = default)
    {
        var rule = _exclusions.Value.Match(relativePath) ?? (otherPath != null ? _exclusions.Value.Match(otherPath) : null);
        if (rule == null)
        {
            return null;
        }

        logger.LogError("Excluded path in a server action (rule '{Rule}'): {Path}", rule, relativePath);
        return await ReportFailureAsync(deviceId, sessionId, recordId, relativePath, songId, ExclusionMatcher.ErrorMessage(rule), reason, ct);
    }

    /// <summary>
    /// Reports a file that could not be uploaded as an <c>Error</c>, so the failure shows in the session.
    /// The check does not save a record for an upload (the upload itself does), so there is none to link to.
    /// </summary>
    private async Task<ActionResult> ReportUploadFailureAsync(
        long deviceId, long sessionId, SyncFileInfo fileInfo, string errorMessage, CancellationToken ct)
    {
        var result = await ReportFailureAsync(deviceId, sessionId, recordId: null, fileInfo.Path, songId: null, errorMessage, fileInfo.Reason, ct);
        return result with { Source = "Device" };
    }

    /// <summary>
    /// Reports a client action that could not be performed as an <c>Error</c> linked to its record.
    /// The server acknowledges the record, so the commit is not blocked, and does not apply it, so
    /// the server state keeps reflecting what is actually on the device.
    /// </summary>
    public async Task<ActionResult> ReportFailureAsync(
        long deviceId,
        long sessionId,
        long? recordId,
        string relativePath,
        long? songId,
        string errorMessage,
        string? reason,
        CancellationToken ct)
    {
        SyncActionCounts? counts = null;
        try
        {
            counts = await apiClient.ReportSyncErrorAsync(deviceId, sessionId, new ReportSyncErrorCliRequest
            {
                FilePath = relativePath,
                ErrorMessage = errorMessage,
                SongId = songId,
                RecordId = recordId
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to report the error of {Action}: {Path}", recordId.HasValue ? $"record {recordId}" : "the upload", relativePath);
        }

        return new ActionResult("Error", relativePath, Source: "Server", ErrorMessage: errorMessage, Reason: reason, SongId: songId, RecordId: recordId, Counts: counts);
    }

    public async Task<ResolveConflictsActionResult> ActionConflictAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        List<SyncRecordItem> conflictRecords,
        List<SyncRecordItem> updateLocalRecords,
        Action<int>? onFilesResolved = null,
        CancellationToken ct = default,
        SyncOptions? options = null,
        IReadOnlyList<SyncFileInfo>? files = null)
    {
        options ??= new SyncOptions();
        var resolveItems = new List<ConflictResolveItem>();
        foreach (var conflict in conflictRecords)
        {
            if (!conflict.SongId.HasValue)
            {
                logger.LogWarning("Skipping conflict with no SongId: {Path}", conflict.FilePath);
                continue;
            }

            var conflictData = SyncDataDeserialization.DeserializeConflictCheckData(conflict.Data);
            var checksumAlgorithm = conflictData?.ServerChecksumAlgorithm;
            var checksum = await ComputeLocalChecksumAsync(ResolveItemKind.Conflict, repositoryPath, conflict.FilePath, checksumAlgorithm, ct);
            if (checksum is null)
            {
                continue;
            }

            resolveItems.Add(new ConflictResolveItem
            {
                Path = conflict.FilePath,
                SongId = conflict.SongId.Value,
                Checksum = checksum,
                ChecksumAlgorithm = checksumAlgorithm!,
                LocalModifiedAt = conflictData?.LocalModifiedAt ?? DateTime.UtcNow
            });
        }

        var potentialUpdateItems = new List<PotentialUpdateResolveItem>();
        foreach (var update in updateLocalRecords)
        {
            if (!update.SongId.HasValue)
            {
                logger.LogWarning("Skipping potential update with no SongId: {Path}", update.FilePath);
                continue;
            }

            var updateData = SyncDataDeserialization.DeserializeUpdateLocalCheckData(update.Data);
            var checksumAlgorithm = updateData?.ServerChecksumAlgorithm;
            var checksum = await ComputeLocalChecksumAsync(ResolveItemKind.PotentialUpdate, repositoryPath, update.FilePath, checksumAlgorithm, ct);
            if (checksum is null)
            {
                continue;
            }

            potentialUpdateItems.Add(new PotentialUpdateResolveItem
            {
                Path = update.FilePath,
                SongId = update.SongId.Value,
                Checksum = checksum,
                ChecksumAlgorithm = checksumAlgorithm!,
                LocalModifiedAt = updateData?.LocalModifiedAt ?? DateTime.UtcNow,
                LastSyncedAt = updateData?.LastSyncedAt ?? DateTime.UtcNow
            });
        }

        if (resolveItems.Count == 0 && potentialUpdateItems.Count == 0)
        {
            return new ResolveConflictsActionResult(Records: [], Counts: SyncActionCounts.Empty);
        }

        var pendingItems = InterleaveResolveItems(resolveItems, potentialUpdateItems);
        logger.LogInformation(
            "Resolving {ConflictCount} conflicts and {PotentialUpdateCount} potential updates",
            resolveItems.Count, potentialUpdateItems.Count);

        var allRecords = new List<SyncRecordItem>();
        var aggregatedCounts = SyncActionCounts.Empty;
        var uploadedPaths = new List<string>();

        try
        {
            var chunkSize = _resolveChunkSize.Value;

            for (var offset = 0; offset < pendingItems.Count;)
            {
                var chunk = TakeResolveChunk(pendingItems, offset, chunkSize.Current);
                var chunkItemCount = chunk.Conflicts.Count + chunk.PotentialUpdates.Count;
                offset += chunkItemCount;

                var resolveStartedAt = Stopwatch.GetTimestamp();
                var resolveResponse = await apiClient.ResolveConflictsAsync(deviceId, sessionId, new ResolveConflictsRequest
                {
                    Conflicts = chunk.Conflicts,
                    PotentialUpdates = chunk.PotentialUpdates
                }, ct);
                chunkSize.Report(chunkItemCount, Stopwatch.GetElapsedTime(resolveStartedAt));

                allRecords.AddRange(resolveResponse.Records);
                aggregatedCounts = aggregatedCounts.Add(resolveResponse.Counts);

                // Real conflicts are settled by the user: keep the local file (upload), take the server's
                // (download), or leave the conflict unresolved (skip)
                var downloadRecordIds = new List<long>();

                foreach (var record in resolveResponse.Records)
                {
                    var logMessage = record.Action switch
                    {
                        SyncRecordAction.Conflict => $"Conflict for {record.FilePath}: {record.Reason}",
                        SyncRecordAction.UpdateTimestamp => $"Resolved conflict for {record.FilePath}: {record.Reason}",
                        SyncRecordAction.UpdateLocal => $"Created UpdateLocal action for record {record.Id}",
                        SyncRecordAction.Rename => $"Created Rename action for record {record.Id}",
                        SyncRecordAction.Error => $"Error for {record.FilePath}: {record.Reason}",
                        _ => $"Created {record.Action} action for record {record.Id}"
                    };
                    logger.LogInformation("{LogMessage}", logMessage);

                    if (record.Action != SyncRecordAction.Conflict)
                    {
                        continue;
                    }

                    var resolution = await ChooseConflictResolutionAsync(record.FilePath, options, ct);
                    if (resolution == ConflictResolution.Download)
                    {
                        downloadRecordIds.Add(record.Id);
                    }
                    else if (resolution == ConflictResolution.Upload)
                    {
                        var uploadResult = await UploadConflictedFileAsync(deviceId, sessionId, repositoryPath, record, files, ct);
                        allRecords.AddRange(uploadResult.Records ?? []);
                        aggregatedCounts = aggregatedCounts.Add(uploadResult.Counts ?? SyncActionCounts.Empty);

                        if (uploadResult.Action != "Error")
                        {
                            uploadedPaths.Add(record.FilePath);
                        }
                    }
                }

                if (downloadRecordIds.Count > 0)
                {
                    var choicesResponse = await apiClient.ChooseConflictsAsync(deviceId, sessionId, downloadRecordIds, ct);
                    allRecords.AddRange(choicesResponse.Records);
                    aggregatedCounts = aggregatedCounts.Add(choicesResponse.Counts);
                }

                // Report the files this request settled
                onFilesResolved?.Invoke(chunkItemCount);
            }

            return new ResolveConflictsActionResult(
                Records: allRecords,
                Counts: aggregatedCounts,
                UploadedPaths: uploadedPaths);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve conflicts");
            return new ResolveConflictsActionResult(Records: allRecords, Counts: aggregatedCounts, UploadedPaths: uploadedPaths);
        }
    }

    /// <summary>
    /// Whether the user lets a local file be deleted: the answer given for every deletion, or the one given now.
    /// </summary>
    private async Task<bool> ConfirmDeletionAsync(string relativePath, CancellationToken ct)
    {
        if (_deletionForAll is { } forAll)
        {
            return forAll;
        }

        var answer = await userPrompt.ConfirmDeletionAsync(relativePath, ct);
        if (answer.ApplyToAll)
        {
            _deletionForAll = answer.Value;
        }

        return answer.Value;
    }

    /// <summary>
    /// The resolution of a real conflict: the one given in the options, or the user's answer (asked once, when
    /// given for every conflict). A direction that never changes one side cannot pick it, so the conflict is
    /// skipped instead.
    /// </summary>
    private async Task<ConflictResolution> ChooseConflictResolutionAsync(string filePath, SyncOptions options, CancellationToken ct)
    {
        ConflictResolution[] choices = options.Direction switch
        {
            SyncDirection.Up => [ConflictResolution.Upload, ConflictResolution.Skip],
            SyncDirection.Down => [ConflictResolution.Download, ConflictResolution.Skip],
            _ => [ConflictResolution.Upload, ConflictResolution.Download, ConflictResolution.Skip],
        };

        var resolution = options.Conflicts ?? _conflictForAll;
        if (resolution == null)
        {
            var answer = await userPrompt.PromptConflictResolutionAsync(filePath, choices, ct);
            if (answer.ApplyToAll)
            {
                _conflictForAll = answer.Value;
            }

            resolution = answer.Value;
        }

        return choices.Contains(resolution.Value) ? resolution.Value : ConflictResolution.Skip;
    }

    /// <summary>
    /// Uploads the local file of a conflict the user resolved by keeping it. The records the server creates
    /// for the file point to the conflict; a failed upload leaves it unresolved.
    /// </summary>
    private async Task<ActionResult> UploadConflictedFileAsync(
        long deviceId, long sessionId, string repositoryPath, SyncRecordItem conflict,
        IReadOnlyList<SyncFileInfo>? files, CancellationToken ct)
    {
        var localModifiedAt = SyncDataDeserialization.DeserializeConflictCheckData(conflict.Data)?.LocalModifiedAt ?? DateTime.UtcNow;
        var scannedFile = files?.FirstOrDefault(f => f.Path == conflict.FilePath);
        var fileInfo = new SyncFileInfo
        {
            Path = conflict.FilePath,
            ModifiedAt = scannedFile?.ModifiedAt ?? localModifiedAt,
            CreatedAt = scannedFile?.CreatedAt ?? localModifiedAt,
            Reason = "Conflict resolved by user: local version wins",
        };

        var result = await ActionUpdateRemoteAsync(deviceId, sessionId, repositoryPath, fileInfo, ct, conflict.Id);

        if (result.Action == "Error")
        {
            logger.LogError("Failed to upload the local version of {Path}: {Error}", conflict.FilePath, result.ErrorMessage ?? result.Reason);
        }

        return result;
    }

    /// <summary>
    /// Computes the checksum of a local file for conflict resolution, with the algorithm the server
    /// uses for the song. Returns <c>null</c>, after logging, when the file is missing, cannot be
    /// read, or the algorithm is unknown, so the other items are still resolved.
    /// </summary>
    private async Task<string?> ComputeLocalChecksumAsync(ResolveItemKind kind, string repositoryPath, string relativePath, string? algorithm, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(algorithm))
        {
            logger.LogError("No checksum algorithm given for {Kind} resolution: {Path}", kind, relativePath);
            return null;
        }

        var fullPath = Path.Combine(repositoryPath, relativePath);
        if (!fileSystem.File.Exists(fullPath))
        {
            logger.LogWarning("{Kind} file not found locally: {Path}", kind, relativePath);
            return null;
        }

        try
        {
            return await fileOps.ComputeChecksumAsync(fullPath, algorithm, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to compute the checksum for {Kind} resolution: {Path}", kind, relativePath);
            return null;
        }
    }

    /// <summary>
    /// Orders the conflict + potential-update items for their requests. Items are interleaved so neither
    /// list is starved when one is much larger than the other.
    /// </summary>
    private static List<ResolveItem> InterleaveResolveItems(
        List<ConflictResolveItem> conflicts,
        List<PotentialUpdateResolveItem> potentialUpdates)
    {
        var items = new List<ResolveItem>(conflicts.Count + potentialUpdates.Count);

        // Interleave by index so a long conflict list doesn't defer all potential updates
        var maxIndex = Math.Max(conflicts.Count, potentialUpdates.Count);
        for (var i = 0; i < maxIndex; i++)
        {
            if (i < conflicts.Count)
            {
                items.Add(new ResolveItem(conflicts[i], null));
            }

            if (i < potentialUpdates.Count)
            {
                items.Add(new ResolveItem(null, potentialUpdates[i]));
            }
        }

        return items;
    }

    /// <summary>
    /// The next request chunk: at most <paramref name="size"/> of the items from <paramref name="offset"/> on.
    /// Each item only carries a checksum, so the size just keeps a single request (one server transaction) bounded.
    /// </summary>
    private static ResolveChunk TakeResolveChunk(List<ResolveItem> items, int offset, int size)
    {
        var chunk = new ResolveChunk();

        foreach (var item in items.Skip(offset).Take(size))
        {
            if (item.Conflict != null)
            {
                chunk.Conflicts.Add(item.Conflict);
            }
            else
            {
                chunk.PotentialUpdates.Add(item.PotentialUpdate!);
            }
        }

        return chunk;
    }

    /// <summary>
    /// The kind of item sent for conflict resolution, used in log messages.
    /// </summary>
    private enum ResolveItemKind
    {
        Conflict,
        PotentialUpdate
    }

    /// <summary>An item of a resolve request: either a conflict or a potential update.</summary>
    private sealed record ResolveItem(ConflictResolveItem? Conflict, PotentialUpdateResolveItem? PotentialUpdate);

    private sealed class ResolveChunk
    {
        public List<ConflictResolveItem> Conflicts { get; } = [];
        public List<PotentialUpdateResolveItem> PotentialUpdates { get; } = [];
    }
}

/// <param name="UploadedPaths">Paths of the conflicts the user resolved by uploading the local file.</param>
public record ResolveConflictsActionResult(List<SyncRecordItem> Records, SyncActionCounts Counts, List<string>? UploadedPaths = null);

internal record ConflictCheckData(DateTime LocalModifiedAt, DateTime ServerModifiedAt, string? ServerChecksumAlgorithm = null);
internal record UpdateLocalCheckData(DateTime LocalModifiedAt, DateTime ServerModifiedAt, DateTime LastSyncedAt, string? ServerChecksumAlgorithm = null);

internal static class SyncDataDeserialization
{
    internal static ConflictCheckData? DeserializeConflictCheckData(System.Text.Json.JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
        try { return data.Value.Deserialize(SyncDataJsonContext.Default.ConflictCheckData); }
        catch { return null; }
    }

    internal static UpdateLocalCheckData? DeserializeUpdateLocalCheckData(System.Text.Json.JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
        try { return data.Value.Deserialize(SyncDataJsonContext.Default.UpdateLocalCheckData); }
        catch { return null; }
    }
}