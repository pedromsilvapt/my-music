namespace MyMusic.CLI.Services.Sync;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using Refit;
using SyncOptions = MyMusic.CLI.Services.Sync.Types.SyncOptions;
using PendingActionItem = MyMusic.CLI.Services.Sync.Types.PendingActionItem;
using AcknowledgeActionRequest = MyMusic.CLI.Services.Sync.Types.AcknowledgeActionRequest;

public class CliFileOps(IFileSystem fileSystem) : IFileOps
{
    public bool FileExists(string path) => fileSystem.File.Exists(path);
    public bool DirectoryExists(string path) => fileSystem.Directory.Exists(path);

    public Task EnsureDirectoryAsync(string path, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
        {
            fileSystem.Directory.CreateDirectory(directory);
        }
        return Task.CompletedTask;
    }

    public async Task WriteFileAsync(string path, Stream content, CancellationToken ct = default)
    {
        await using var fileStream = fileSystem.FileStream.New(path, FileMode.Create);
        await content.CopyToAsync(fileStream, ct);
    }

    public Task DeleteFileAsync(string path, CancellationToken ct = default)
    {
        if (fileSystem.File.Exists(path))
        {
            fileSystem.File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public Task<string> ReadFileBase64Async(string path, CancellationToken ct = default)
    {
        var bytes = fileSystem.File.ReadAllBytes(path);
        return Task.FromResult(Convert.ToBase64String(bytes));
    }

    public Task<DateTime?> GetModificationTimeAsync(string path, CancellationToken ct = default)
    {
        if (!fileSystem.File.Exists(path))
        {
            return Task.FromResult<DateTime?>(null);
        }
        var fileInfo = fileSystem.FileInfo.New(path);
        return Task.FromResult<DateTime?>(fileInfo.LastWriteTimeUtc);
    }

    public void CleanupEmptyParentDirectories(string filePath, string repositoryRoot)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir) && dir != repositoryRoot && dir.TrimEnd(Path.DirectorySeparatorChar).Length > repositoryRoot.Length)
        {
            if (fileSystem.Directory.Exists(dir) && !fileSystem.Directory.EnumerateFileSystemEntries(dir).Any())
            {
                fileSystem.Directory.Delete(dir);
            }
            else
            {
                break;
            }
            dir = Path.GetDirectoryName(dir);
        }
    }
}

public class CliKeepAwake : IKeepAwake
{
    public Task ActivateAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Deactivate() { }
}

public class CliUserPrompt : IUserPrompt
{
    public Task<ConflictResolution> PromptConflictResolutionAsync(string filePath, CancellationToken ct = default)
    {
        Console.Write($"Conflict detected for '{filePath}'. Upload, download, or skip? [u/d/s]: ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        var resolution = response switch
        {
            "u" or "upload" => ConflictResolution.Upload,
            "d" or "download" => ConflictResolution.Download,
            _ => ConflictResolution.Skip
        };
        return Task.FromResult(resolution);
    }

    public Task<bool> ConfirmDeletionAsync(string filePath, CancellationToken ct = default)
    {
        Console.Write($"Delete '{filePath}'? [y/N]: ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return Task.FromResult(response == "y" || response == "yes");
    }
}

public class CliSyncConfig(
    IOptions<MyMusicOptions> options,
    IMyMusicClient client,
    ILogger<CliSyncConfig> logger) : ISyncConfig
{
    private long? _deviceId;

    public async Task<long?> GetDeviceIdAsync(CancellationToken ct = default)
    {
        if (_deviceId.HasValue)
        {
            return _deviceId.Value;
        }

        try
        {
            var devicesResponse = await client.GetDevicesAsync(ct);
            var existingDevice = devicesResponse.Devices.FirstOrDefault(d => d.Name == options.Value.Device.Name);

            if (existingDevice is not null)
            {
                logger.LogInformation("Found existing device: {DeviceName} (ID: {DeviceId})",
                    existingDevice.Name, existingDevice.Id);

                var needsUpdate = existingDevice.Icon != options.Value.Device.Icon ||
                                  existingDevice.Color != options.Value.Device.Color ||
                                  existingDevice.NamingTemplate != options.Value.Device.NamingTemplate ||
                                  existingDevice.ImportOnPurchase != options.Value.Device.ImportOnPurchase;

                if (needsUpdate)
                {
                    logger.LogInformation("Updating device properties for: {DeviceName}", existingDevice.Name);
                    await client.UpdateDeviceAsync(existingDevice.Id, new UpdateDeviceRequest
                    {
                        Icon = options.Value.Device.Icon,
                        Color = options.Value.Device.Color,
                        NamingTemplate = options.Value.Device.NamingTemplate,
                        ImportOnPurchase = options.Value.Device.ImportOnPurchase,
                    }, ct);
                }

                _deviceId = existingDevice.Id;
                return _deviceId;
            }

            logger.LogInformation("Creating new device: {DeviceName}", options.Value.Device.Name);
            var newDevice = await client.CreateDeviceAsync(new CreateDeviceRequest
            {
                Name = options.Value.Device.Name,
                Icon = options.Value.Device.Icon,
                Color = options.Value.Device.Color,
                NamingTemplate = options.Value.Device.NamingTemplate,
                ImportOnPurchase = options.Value.Device.ImportOnPurchase,
            }, ct);
            logger.LogInformation("Created device with ID: {DeviceId}", newDevice.Device.Id);
            _deviceId = newDevice.Device.Id;
            return _deviceId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get or create device");
            return null;
        }
    }

    public string GetRepositoryPath() => options.Value.Repository.Path;
    public string[] GetMusicExtensions() => options.Value.Repository.MusicExtensions.ToArray();
    public string[] GetExcludePatterns() => options.Value.Repository.ExcludePatterns.ToArray();
    public int GetChunkSize() => options.Value.Sync.ChunkSize;
    public Task<int?> GetLastScanTotalAsync(CancellationToken ct = default) => Task.FromResult<int?>(null);
    public Task SetLastScanTotalAsync(int count, CancellationToken ct = default) => Task.CompletedTask;
    public Task SetLastSyncAtAsync(DateTime date, CancellationToken ct = default) => Task.CompletedTask;
}

public class CliSyncApiClient(IMyMusicClient client) : ISyncApiClient
{
    public async Task<StartSyncResult> StartSyncAsync(long deviceId, StartSyncRequest request, CancellationToken ct = default)
    {
        var response = await client.StartSyncAsync(deviceId, new SyncStartRequest
        {
            DryRun = request.DryRun,
            RepositoryPath = request.RepositoryPath
        }, ct);
        return new StartSyncResult { SessionId = response.SessionId };
    }

    public async Task<CheckSyncResult> CheckSyncAsync(long deviceId, CheckSyncRequest request, CancellationToken ct = default)
    {
        var syncFiles = request.Files.Select(f => new SyncFileInfoItem
        {
            Path = f.Path,
            ModifiedAt = f.ModifiedAt,
            CreatedAt = f.CreatedAt,
            Reason = f.Reason
        }).ToList();

        var response = await client.CheckSyncAsync(deviceId, new SyncCheckRequest
        {
            Files = syncFiles,
            Force = request.Force
        }, ct);

        return new CheckSyncResult
        {
            ToCreate = response.ToCreate.Select(f => new SyncFileInfo
            {
                Path = f.Path,
                ModifiedAt = f.ModifiedAt,
                CreatedAt = f.CreatedAt,
                Reason = f.Reason
            }).ToList(),
            ToUpdate = response.ToUpdate.Select(f => new SyncFileInfo
            {
                Path = f.Path,
                ModifiedAt = f.ModifiedAt,
                CreatedAt = f.CreatedAt,
                Reason = f.Reason
            }).ToList(),
            PotentialConflicts = response.PotentialConflicts.Select(c => new PotentialConflictItem
            {
                Path = c.Path,
                LocalModifiedAt = c.LocalModifiedAt,
                ServerModifiedAt = c.ServerModifiedAt,
                LastSyncedAt = c.LastSyncedAt,
                SongId = c.SongId,
                ServerChecksum = c.ServerChecksum,
                ServerChecksumAlgorithm = c.ServerChecksumAlgorithm
            }).ToList(),
            PendingActions = response.PendingActions.Select(a => new PendingActionItem
            {
                SongId = a.SongId,
                Path = a.Path,
                Action = a.Action,
                PreviousPath = a.PreviousPath
            }).ToList()
        };
    }

    public async Task<UploadFileResult> UploadFileAsync(long deviceId, UploadFileRequest request, CancellationToken ct = default)
    {
        var streamPart = new StreamPart(request.FileStream, request.FileName);
        var response = await client.UploadFileAsync(deviceId, streamPart, request.Path, request.ModifiedAt, request.CreatedAt, ct);
        return new UploadFileResult
        {
            Success = true,
            SongId = response.SongId,
            PendingActions = response.PendingActions.Select(a => new PendingActionItem
            {
                SongId = a.SongId,
                Path = a.Path,
                Action = a.Action,
                PreviousPath = a.PreviousPath
            }).ToList()
        };
    }

    public async Task RecordChunkAsync(long deviceId, long sessionId, RecordChunkRequest request, CancellationToken ct = default)
    {
        var records = request.Records.Select(r => new SyncRecordRequestItem
        {
            FilePath = r.FilePath,
            Action = r.Action,
            SongId = r.SongId,
            ErrorMessage = r.ErrorMessage,
            Source = r.Source,
            Reason = r.Reason
        }).ToList();

        await client.RecordChunkAsync(deviceId, sessionId, new SyncRecordsRequest { Records = records }, ct);
    }

    public async Task<CompleteSyncResult> CompleteSyncAsync(long deviceId, long sessionId, CompleteSyncRequest request, CancellationToken ct = default)
    {
        var response = await client.CompleteSyncAsync(deviceId, sessionId, new SyncCompleteRequest { Direction = request.Direction }, ct);
        return new CompleteSyncResult
        {
            CreatedCount = response.CreatedCount,
            UpdatedCount = response.UpdatedCount,
            SkippedCount = response.SkippedCount,
            DownloadedCount = response.DownloadedCount,
            RemovedCount = response.RemovedCount,
            ErrorCount = response.ErrorCount
        };
    }

    public async Task<GetPendingActionsResult> GetPendingActionsAsync(long deviceId, CancellationToken ct = default)
    {
        var response = await client.GetPendingActionsAsync(deviceId, ct);
        return new GetPendingActionsResult
        {
            Actions = response.Actions.Select(a => new PendingActionItem
            {
                SongId = a.SongId,
                Path = a.Path,
                Action = a.Action,
                PreviousPath = a.PreviousPath
            }).ToList()
        };
    }

    public async Task AcknowledgeActionAsync(long deviceId, AcknowledgeActionRequest request, CancellationToken ct = default)
    {
        await client.AcknowledgeActionAsync(deviceId, new Api.Dtos.AcknowledgeActionRequest
        {
            DevicePath = request.DevicePath,
            ModifiedAt = request.ModifiedAt,
            PreviousDevicePath = request.PreviousDevicePath
        }, ct);
    }

    public async Task<ResolveConflictsResult> ResolveConflictsAsync(long deviceId, ResolveConflictsRequest request, CancellationToken ct = default)
    {
        var conflicts = request.Conflicts.Select(c => new SyncConflictResolveItem
        {
            Path = c.Path,
            SongId = c.SongId,
            FileContentBase64 = c.FileContentBase64,
            LocalModifiedAt = DateTime.Parse(c.LocalModifiedAt)
        }).ToList();

        var response = await client.ResolveConflictsAsync(deviceId, new SyncResolveConflictsRequest { Conflicts = conflicts }, ct);

        return new ResolveConflictsResult
        {
            ToUpload = response.ToUpload.Select(f => new SyncFileInfo
            {
                Path = f.Path,
                ModifiedAt = f.ModifiedAt,
                CreatedAt = f.CreatedAt,
                Reason = f.Reason
            }).ToList(),
            Resolved = response.Resolved.Select(r => new ResolvedConflictItem
            {
                Path = r.Path,
                ModifiedAt = r.ModifiedAt,
                CreatedAt = r.CreatedAt,
                Reason = r.Reason
            }).ToList(),
            Conflicts = response.Conflicts.Select(c => new SyncConflictItem
            {
                Path = c.Path,
                Reason = c.Reason
            }).ToList()
        };
    }

    public async Task<Stream> DownloadSongAsync(long songId, CancellationToken ct = default)
    {
        return await client.DownloadSongAsync(songId, ct);
    }
}

public class CliSyncState(SyncOptions options, CancellationToken ct) : ISyncState
{
    public bool IsCancelled => ct.IsCancellationRequested;
    public SyncOptions Options => options;
}

public class CliFileSystemScanner(IFileScanner fileScanner) : IFileSystemScanner
{
    public async Task<ScanResult> ScanAsync(
        string rootPath,
        string[] extensions,
        string[] excludePatterns,
        Action<int, string>? onProgress = null,
        Action<string, string>? onError = null,
        CancellationToken ct = default)
    {
        var repoOptions = new RepositoryOptions
        {
            Path = rootPath,
            MusicExtensions = extensions.ToList(),
            ExcludePatterns = excludePatterns.ToList()
        };

        var result = await fileScanner.ScanAsync(rootPath, repoOptions, onProgress, onError, ct);
        return new ScanResult
        {
            Files = result.Files.Select(f => new ScannedFile
            {
                RelativePath = f.RelativePath,
                FullPath = f.FullPath,
                ModifiedAt = f.ModifiedAt,
                CreatedAt = f.CreatedAt,
                Size = 0
            }).ToList(),
            Errors = result.Errors.Select(e => new ScanError
            {
                Path = e.Path,
                Error = e.Error
            }).ToList()
        };
    }
}
