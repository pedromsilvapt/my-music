namespace MyMusic.CLI.Services.Sync;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync.Types;

public class SyncActionsDevice(
    IFileOps fileOps,
    ISyncApiClient apiClient,
    IUserPrompt userPrompt,
    IFileSystem fileSystem,
    ILogger<SyncActionsDevice> logger)
{
    public record ActionResult(
        string Action,
        string FilePath,
        string Source = "Device",
        string? Reason = null,
        string? ErrorMessage = null,
        long? SongId = null,
        long? RecordId = null,
        SyncActionCounts? Counts = null);

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
            return new ActionResult("Error", fileInfo.Path, Reason: "File not found");
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

            return new ActionResult("Created", fileInfo.Path, Reason: fileInfo.Reason, SongId: uploadResult.SongId, Counts: uploadResult.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload file: {Path}", fileInfo.Path);
            return new ActionResult("Error", fileInfo.Path, ErrorMessage: ex.Message, Reason: fileInfo.Reason);
        }
    }

    public async Task<ActionResult> ActionUpdateRemoteAsync(
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
            return new ActionResult("Error", fileInfo.Path, Reason: "File not found");
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

            return new ActionResult("Updated", fileInfo.Path, Reason: fileInfo.Reason, SongId: uploadResult.SongId, Counts: uploadResult.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update file: {Path}", fileInfo.Path);
            return new ActionResult("Error", fileInfo.Path, ErrorMessage: ex.Message, Reason: fileInfo.Reason);
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
        var fullPath = Path.Combine(repositoryPath, relativePath);
        var fileExists = fileOps.FileExists(fullPath);

        if (fileExists)
        {
            logger.LogError("File already exists during create: {Path}", relativePath);
            return new ActionResult("Error", relativePath, Source: "Server", ErrorMessage: "File already exists", Reason: "Unexpected local file during create");
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
        CancellationToken ct = default)
    {
        var fullPath = Path.Combine(repositoryPath, relativePath);
        var fileExists = fileOps.FileExists(fullPath);

        if (!fileExists)
        {
            logger.LogError("File not found during update: {Path}", relativePath);
            return new ActionResult("Error", relativePath, Source: "Server", ErrorMessage: "File not found", Reason: "Missing local file during update");
        }

        return await DownloadAndAckAsync(deviceId, sessionId, repositoryPath, songId, relativePath, dryRun, recordId, reason, isUpdate: true, ct);
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
        CancellationToken ct)
    {
        var actionName = isUpdate ? "UpdateLocal" : "CreateLocal";
        var baseReason = reason ?? (isUpdate ? "Server-initiated update" : "Server-initiated download");
        var fullPath = Path.Combine(repositoryPath, relativePath);

        if (dryRun)
        {
            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
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

        var tempPath = fullPath + ".tmp";
        try
        {
            await fileOps.EnsureDirectoryAsync(fullPath, ct);

            await using var stream = await apiClient.DownloadSongAsync(songId!.Value, ct);
            await fileOps.WriteFileAsync(tempPath, stream, ct);

            if (isUpdate)
            {
                await fileOps.DeleteFileAsync(fullPath, ct);
            }

            await fileOps.MoveFileAsync(tempPath, fullPath, ct);

            var modifiedAt = await fileOps.GetModificationTimeAsync(fullPath, ct);
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
            return new ActionResult("Error", relativePath, Source: "Server", ErrorMessage: ex.Message, Reason: $"{baseReason} failed", SongId: songId);
        }
        finally
        {
            if (fileOps.FileExists(tempPath))
            {
                await fileOps.DeleteFileAsync(tempPath, ct);
            }
        }
    }

    public async Task<ActionResult?> ActionDeleteAsync(
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
            var confirmed = await userPrompt.ConfirmDeletionAsync(relativePath, ct);
            if (!confirmed)
            {
                return null;
            }
        }

        var baseReason = reason ?? "Server-initiated removal";

        if (dryRun)
        {
            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
            }, ct);

            return new ActionResult("Delete", relativePath, Source: "Server", Reason: baseReason, SongId: songId, RecordId: recordId, Counts: ackResult.Counts);
        }

        try
        {
            await fileOps.DeleteFileAsync(fullPath, ct);
            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
            }, ct);

            return new ActionResult("Delete", relativePath, Source: "Server", Reason: baseReason, SongId: songId, RecordId: recordId, Counts: ackResult.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete file: {Path}", relativePath);
            return new ActionResult("Error", relativePath, Source: "Server", ErrorMessage: ex.Message, Reason: $"{baseReason} failed", SongId: songId);
        }
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
        var fullPath = Path.Combine(repositoryPath, relativePath);
        var previousFullPath = Path.Combine(repositoryPath, previousRelativePath);

        if (dryRun)
        {
            var ackResult = await apiClient.AcknowledgeActionAsync(deviceId, sessionId, new AcknowledgeActionRequest
            {
                RecordIds = [recordId]
            }, ct);

            return new ActionResult("Renamed", relativePath, Source: "Server", Reason: $"Renamed from '{previousRelativePath}'", RecordId: recordId, Counts: ackResult.Counts);
        }

        try
        {
            if (fileOps.FileExists(previousFullPath))
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
            return new ActionResult("Error", relativePath, Source: "Server", ErrorMessage: ex.Message, Reason: $"Rename from '{previousRelativePath}' failed");
        }
    }

    public async Task<ResolveConflictsActionResult> ActionConflictAsync(
        long deviceId,
        long sessionId,
        string repositoryPath,
        List<PotentialConflictItem> conflicts,
        List<PotentialUpdateItem> potentialUpdates,
        bool dryRun,
        CancellationToken ct = default)
    {
        if (dryRun)
        {
            logger.LogInformation("Dry-run: skipping conflict resolution for {ConflictCount} conflicts and {UpdateCount} potential updates", conflicts.Count, potentialUpdates.Count);
            return new ResolveConflictsActionResult(Conflicts: conflicts.Count, ToUpdatePaths: [], UpdateLocalRecords: [], RenameRecords: []);
        }

        var resolveItems = new List<ConflictResolveItem>();
        foreach (var conflict in conflicts)
        {
            if (!conflict.SongId.HasValue)
            {
                logger.LogWarning("Skipping conflict with no SongId: {Path}", conflict.Path);
                continue;
            }

            var fullPath = Path.Combine(repositoryPath, conflict.Path);
            if (!fileSystem.File.Exists(fullPath))
            {
                logger.LogWarning("Conflict file not found locally: {Path}", conflict.Path);
                continue;
            }

            var fileContentBase64 = await fileOps.ReadFileBase64Async(fullPath, ct);
            resolveItems.Add(new ConflictResolveItem
            {
                Path = conflict.Path,
                SongId = conflict.SongId.Value,
                FileContentBase64 = fileContentBase64,
                LocalModifiedAt = conflict.LocalModifiedAt.ToUniversalTime()
            });
        }

        var potentialUpdateItems = new List<PotentialUpdateResolveItem>();
        foreach (var update in potentialUpdates)
        {
            var fullPath = Path.Combine(repositoryPath, update.Path);
            if (!fileSystem.File.Exists(fullPath))
            {
                logger.LogWarning("Potential update file not found locally: {Path}", update.Path);
                continue;
            }

            var fileContentBase64 = await fileOps.ReadFileBase64Async(fullPath, ct);
            potentialUpdateItems.Add(new PotentialUpdateResolveItem
            {
                Path = update.Path,
                SongId = update.SongId,
                FileContentBase64 = fileContentBase64,
                LocalModifiedAt = update.LocalModifiedAt.ToUniversalTime(),
                LastSyncedAt = update.LastSyncedAt.ToUniversalTime()
            });
        }

        if (resolveItems.Count == 0 && potentialUpdateItems.Count == 0)
        {
            return new ResolveConflictsActionResult(Conflicts: 0, ToUpdatePaths: [], UpdateLocalRecords: [], RenameRecords: []);
        }

        try
        {
            var resolveResponse = await apiClient.ResolveConflictsAsync(deviceId, sessionId, new ResolveConflictsRequest
            {
                Conflicts = resolveItems,
                PotentialUpdates = potentialUpdateItems
            }, ct);

            var toUpdatePaths = resolveResponse.ToUpload.Select(f => f.Path).ToHashSet();

            foreach (var resolved in resolveResponse.Resolved)
            {
                logger.LogInformation("Resolved conflict for {Path}: {Reason}", resolved.Path, resolved.Reason);
            }

            foreach (var record in resolveResponse.UpdateLocalRecords)
            {
                logger.LogInformation("Created UpdateLocal action for record {RecordId}", record.Id);
            }

            foreach (var record in resolveResponse.RenameRecords)
            {
                logger.LogInformation("Created Rename action for record {RecordId}", record.Id);
            }

            var errorCount = resolveResponse.Conflicts.Count;

            return new ResolveConflictsActionResult(
                Conflicts: errorCount,
                ToUpdatePaths: toUpdatePaths,
                UpdateLocalRecords: resolveResponse.UpdateLocalRecords,
                RenameRecords: resolveResponse.RenameRecords,
                Counts: resolveResponse.Counts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve conflicts");
            return new ResolveConflictsActionResult(Conflicts: conflicts.Count, ToUpdatePaths: [], UpdateLocalRecords: [], RenameRecords: []);
        }
    }
}

public record ResolveConflictsActionResult(int Conflicts, HashSet<string> ToUpdatePaths, List<SyncActionRecordItem> UpdateLocalRecords, List<SyncActionRecordItem> RenameRecords, SyncActionCounts? Counts = null);