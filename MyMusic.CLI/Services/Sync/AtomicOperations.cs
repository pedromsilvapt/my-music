namespace MyMusic.CLI.Services.Sync;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;

public class AtomicOperations(
    IFileSystem fileSystem,
    ISyncApiClient apiClient,
    IFileOps fileOps,
    IUserPrompt userPrompt,
    ILogger<AtomicOperations> logger)
{
    public async Task<RecordItem> UploadOneFileAsync(
        long deviceId,
        string repositoryPath,
        SyncFileInfo fileInfo,
        bool dryRun,
        CancellationToken ct = default)
    {
        var fullPath = Path.Combine(repositoryPath, fileInfo.Path);
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

        if (dryRun)
        {
            return new RecordItem
            {
                FilePath = fileInfo.Path,
                Action = "Created",
                Source = "Device",
                Reason = fileInfo.Reason
            };
        }

        try
        {
            await using var stream = fileSystem.File.OpenRead(fullPath);
            var fileName = Path.GetFileName(fullPath);
            await apiClient.UploadFileAsync(deviceId, new UploadFileRequest
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
                Action = "Created",
                Source = "Device",
                Reason = fileInfo.Reason
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload file: {Path}", fileInfo.Path);
            return new RecordItem
            {
                FilePath = fileInfo.Path,
                Action = "Error",
                ErrorMessage = ExtractErrorMessage(ex),
                Source = "Device",
                Reason = fileInfo.Reason
            };
        }
    }

    public async Task<RecordItem?> DownloadOneFileAsync(
        long songId,
        string fullPath,
        string relativePath,
        bool dryRun,
        bool autoConfirm,
        CancellationToken ct = default)
    {
        var fileExists = fileOps.FileExists(fullPath);

        if (!dryRun && fileExists && !autoConfirm)
        {
            var confirmed = await userPrompt.ConfirmDeletionAsync(relativePath, ct);
            if (!confirmed)
            {
                return null;
            }
        }

        if (dryRun)
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

            await using var stream = await apiClient.DownloadSongAsync(songId, ct);
            await fileOps.WriteFileAsync(tempPath, stream, ct);

            if (fileExists)
            {
                await fileOps.DeleteFileAsync(fullPath, ct);
            }

            fileSystem.File.Move(tempPath, fullPath);

            var modifiedAt = await fileOps.GetModificationTimeAsync(fullPath, ct);
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

    public async Task<RecordItem?> RemoveOneFileAsync(
        long deviceId,
        string fullPath,
        string relativePath,
        bool dryRun,
        bool autoConfirm,
        CancellationToken ct = default)
    {
        var fileExists = fileOps.FileExists(fullPath);

        if (!fileExists)
        {
            if (!dryRun)
            {
                await apiClient.AcknowledgeActionAsync(deviceId, new AcknowledgeActionRequest
                {
                    DevicePath = relativePath
                }, ct);
            }
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

        if (dryRun)
        {
            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Removed",
                Source = "Server",
                Reason = "Server-initiated removal"
            };
        }

        try
        {
            await fileOps.DeleteFileAsync(fullPath, ct);
            await apiClient.AcknowledgeActionAsync(deviceId, new AcknowledgeActionRequest
            {
                DevicePath = relativePath
            }, ct);

            return new RecordItem
            {
                FilePath = relativePath,
                Action = "Removed",
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
                ErrorMessage = ex.Message,
                Source = "Server",
                Reason = "Server-initiated removal failed"
            };
        }
    }

    private static string ExtractErrorMessage(Exception ex)
    {
        return ex.Message;
    }
}
