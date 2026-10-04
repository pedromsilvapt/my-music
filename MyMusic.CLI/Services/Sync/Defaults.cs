namespace MyMusic.CLI.Services.Sync;

using System.Globalization;
using System.IO.Abstractions;
using System.IO.Hashing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Devices;
using MyMusic.CLI.Services.Sync.Types;
using MyMusic.CLI.Services.Terminal;
using Refit;
using SyncOptions = MyMusic.CLI.Services.Sync.Types.SyncOptions;
using SyncRecordItem = MyMusic.CLI.Services.Sync.Types.SyncRecordItem;
using SyncRecordSongInfo = MyMusic.CLI.Services.Sync.Types.SyncRecordSongInfo;
using AcknowledgeActionRequest = MyMusic.CLI.Services.Sync.Types.AcknowledgeActionRequest;
using AcknowledgeActionResult = MyMusic.CLI.Services.Sync.Types.AcknowledgeActionResult;
using SyncActionCounts = MyMusic.CLI.Services.Sync.Types.SyncActionCounts;

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

    public Task MoveFileAsync(string fromPath, string toPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(toPath);
        if (!string.IsNullOrEmpty(dir) && !fileSystem.Directory.Exists(dir))
        {
            fileSystem.Directory.CreateDirectory(dir);
        }
        fileSystem.File.Move(fromPath, toPath);
        return Task.CompletedTask;
    }

    public async Task<string> ComputeChecksumAsync(string path, string algorithm, CancellationToken ct = default)
    {
        NonCryptographicHashAlgorithm hash = algorithm switch
        {
            "XxHash128" => new XxHash128(),
            _ => throw new NotSupportedException($"Unsupported checksum algorithm: {algorithm}"),
        };

        await using var stream = fileSystem.File.OpenRead(path);
        await hash.AppendAsync(stream, ct);
        return Convert.ToBase64String(hash.GetCurrentHash());
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

public class CliUserPrompt(ITerminal terminal) : IUserPrompt
{
    public async Task<ConflictResolution> PromptConflictResolutionAsync(string filePath, IReadOnlyList<ConflictResolution> choices, CancellationToken ct = default)
    {
        var names = string.Join(", ", choices.Select(c => c.ToString().ToLowerInvariant()));
        var keys = string.Join("/", choices.Select(c => c.ToString().ToLowerInvariant()[0]));
        var answer = await terminal.AskAsync($"Conflict detected for '{filePath}'. Choose one of: {names} [{keys}]: ", ct);
        var resolution = answer?.Trim().ToLowerInvariant() switch
        {
            "u" or "upload" => ConflictResolution.Upload,
            "d" or "download" => ConflictResolution.Download,
            _ => ConflictResolution.Skip
        };
        return choices.Contains(resolution) ? resolution : ConflictResolution.Skip;
    }

    public async Task<bool> ConfirmDeletionAsync(string filePath, CancellationToken ct = default)
    {
        var answer = await terminal.AskAsync($"Delete '{filePath}'? [y/N]: ", ct);
        var response = answer?.Trim().ToLowerInvariant();
        return response == "y" || response == "yes";
    }
}

public class CliSyncConfig(
    IOptions<MyMusicOptions> options,
    IDeviceConfigService deviceConfig,
    ILogger<CliSyncConfig> logger) : ISyncConfig
{
    private long? _deviceId;

    public async Task<long?> GetDeviceIdAsync(bool saveOptions, CancellationToken ct = default)
    {
        if (_deviceId.HasValue)
        {
            return _deviceId.Value;
        }

        try
        {
            _deviceId = (await deviceConfig.ResolveAsync(saveOptions, ct)).DeviceId;
            return _deviceId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get or create device");
            return null;
        }
    }

    public string? GetNamingTemplate() => options.Value.Device.NamingTemplate;
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
            Direction = request.Direction.ToString(),
            RepositoryPath = request.RepositoryPath,
            Deduplicate = request.Deduplicate,
            ScanErrors = request.ScanErrors?.Select(e => new SyncScanErrorItem
            {
                FilePath = e.Path,
                ErrorMessage = e.Error
            }).ToList(),
            DeviceOptions = request.DeviceOptions is { } deviceOptions
                ? new SyncStartDeviceOptions { NamingTemplate = deviceOptions.NamingTemplate }
                : null
        }, ct);
        return new StartSyncResult { SessionId = response.SessionId };
    }

    public async Task<PrepareDeduplicateResult> PrepareDeduplicateAsync(long deviceId, long sessionId, CancellationToken ct = default)
    {
        var response = await client.PrepareDeduplicateAsync(deviceId, sessionId, ct);
        return new PrepareDeduplicateResult
        {
            Total = response.Total,
            Processed = response.Processed,
            Done = response.Done
        };
    }

    public async Task<CheckSyncResult> CheckSyncAsync(long deviceId, long sessionId, CheckSyncRequest request, CancellationToken ct = default)
    {
        var syncFiles = request.Files.Select(f => new SyncFileInfoItem
        {
            Path = f.Path,
            ModifiedAt = f.ModifiedAt,
            CreatedAt = f.CreatedAt,
            Reason = f.Reason
        }).ToList();

        var response = await client.CheckSyncAsync(deviceId, sessionId, new SyncCheckRequest
        {
            Files = syncFiles,
            Force = request.Force
        }, ct);

        return new CheckSyncResult
        {
            Records = response.Records.Select(ToRecordItem).ToList(),
            Counts = SyncActionCounts.FromApi(response.Counts)
        };
    }

    public async Task<UploadFileResult> UploadFileAsync(long deviceId, long sessionId, UploadFileRequest request, CancellationToken ct = default)
    {
        var streamPart = new StreamPart(request.FileStream, request.FileName);
        var response = await client.UploadFileAsync(deviceId, sessionId, streamPart, request.Path, request.ModifiedAt, request.CreatedAt, request.ResolvesConflictRecordId?.ToString(), ct);
        return new UploadFileResult
        {
            Success = true,
            SongId = response.SongId,
            Records = response.Records.Select(ToRecordItem).ToList(),
            Counts = SyncActionCounts.FromApi(response.Counts)
        };
    }

    private static SyncRecordItem ToRecordItem(SyncRecordResponseItem a) => new()
    {
        Id = a.Id,
        FilePath = a.FilePath,
        Action = a.Action,
        SongId = a.SongId,
        Data = a.Data,
        ResolvesConflictRecordId = a.ResolvesConflictRecordId,
        SongInfo = a.SongInfo is not null ? new SyncRecordSongInfo
        {
            Id = a.SongInfo.Id,
            Title = a.SongInfo.Title,
            ArtistNames = a.SongInfo.ArtistNames,
            CoverId = a.SongInfo.CoverId,
        } : null,
        Reason = a.Reason,
        Acknowledged = a.Acknowledged,
        ProcessedAt = a.ProcessedAt,
    };

    public async Task<CompleteSyncResult> CompleteSyncAsync(long deviceId, long sessionId, CancellationToken ct = default)
    {
        var response = await client.CompleteSyncAsync(deviceId, sessionId, ct);
        return new CompleteSyncResult
        {
            CreateRemoteCount = response.CreateRemoteCount,
            UpdateRemoteCount = response.UpdateRemoteCount,
            SkippedCount = response.SkippedCount,
            CreateLocalCount = response.CreateLocalCount,
            UpdateLocalCount = response.UpdateLocalCount,
            DeleteLocalCount = response.DeleteLocalCount,
            LinkCount = response.LinkCount,
            UnlinkCount = response.UnlinkCount,
            RenameCount = response.RenameCount,
            ConflictCount = response.ConflictCount,
            UpdateTimestampCount = response.UpdateTimestampCount,
            ErrorCount = response.ErrorCount
        };
    }

    public async Task<CommitSyncResult> CommitSyncAsync(long deviceId, long sessionId, CancellationToken ct = default)
    {
        var response = await client.CommitSyncAsync(deviceId, sessionId, ct);
        return new CommitSyncResult
        {
            CreateRemoteCount = response.CreateRemoteCount,
            UpdateRemoteCount = response.UpdateRemoteCount,
            SkippedCount = response.SkippedCount,
            CreateLocalCount = response.CreateLocalCount,
            UpdateLocalCount = response.UpdateLocalCount,
            DeleteLocalCount = response.DeleteLocalCount,
            LinkCount = response.LinkCount,
            UnlinkCount = response.UnlinkCount,
            RenameCount = response.RenameCount,
            ConflictCount = response.ConflictCount,
            UpdateTimestampCount = response.UpdateTimestampCount,
            ErrorCount = response.ErrorCount,
            CommittedAt = response.CommittedAt
        };
    }

    public async Task<CreatePendingActionsResult> CreatePendingActionsAsync(long deviceId, long sessionId, CancellationToken ct = default)
    {
        var response = await client.CreatePendingActionsAsync(deviceId, sessionId, ct);
        return new CreatePendingActionsResult
        {
            Records = response.Records.Select(ToRecordItem).ToList(),
            Counts = SyncActionCounts.FromApi(response.Counts)
        };
    }

    public async Task<AcknowledgeActionResult> AcknowledgeActionAsync(long deviceId, long sessionId, AcknowledgeActionRequest request, CancellationToken ct = default)
    {
        var response = await client.AcknowledgeActionAsync(deviceId, sessionId, new Api.Dtos.AcknowledgeActionRequest
        {
            RecordIds = request.RecordIds,
            ModifiedAt = request.ModifiedAt
        }, ct);

        return new AcknowledgeActionResult
        {
            Success = response.Success,
            Counts = SyncActionCounts.FromApi(response.Counts)
        };
    }

    public async Task<ResolveConflictsResult> ResolveConflictsAsync(long deviceId, long sessionId, ResolveConflictsRequest request, CancellationToken ct = default)
    {
        var conflicts = request.Conflicts.Select(c => new SyncConflictResolveItem
        {
            Path = c.Path,
            SongId = c.SongId,
            Checksum = c.Checksum,
            ChecksumAlgorithm = c.ChecksumAlgorithm,
            LocalModifiedAt = c.LocalModifiedAt.ToUniversalTime()
        }).ToList();

        var potentialUpdates = request.PotentialUpdates.Select(u => new SyncPotentialUpdateResolveItem
        {
            Path = u.Path,
            SongId = u.SongId,
            Checksum = u.Checksum,
            ChecksumAlgorithm = u.ChecksumAlgorithm,
            LocalModifiedAt = u.LocalModifiedAt.ToUniversalTime(),
            LastSyncedAt = u.LastSyncedAt.ToUniversalTime()
        }).ToList();

        var response = await client.ResolveConflictsAsync(deviceId, sessionId, new SyncResolveConflictsRequest { Conflicts = conflicts, PotentialUpdates = potentialUpdates }, ct);

        return new ResolveConflictsResult
        {
            Records = response.Records.Select(ToRecordItem).ToList(),
            Counts = SyncActionCounts.FromApi(response.Counts)
        };
    }

    public async Task<ResolveConflictsResult> ChooseConflictsAsync(long deviceId, long sessionId, IReadOnlyCollection<long> downloadRecordIds, CancellationToken ct = default)
    {
        var response = await client.ChooseConflictsAsync(deviceId, sessionId, new SyncConflictChoicesRequest { DownloadRecordIds = downloadRecordIds.ToList() }, ct);

        return new ResolveConflictsResult
        {
            Records = response.Records.Select(ToRecordItem).ToList(),
            Counts = SyncActionCounts.FromApi(response.Counts)
        };
    }

    public async Task<Stream> DownloadSongAsync(long songId, CancellationToken ct = default)
    {
        return await client.DownloadSongAsync(songId, ct);
    }

    public async Task<SyncActionCounts> ReportSyncErrorAsync(long deviceId, long sessionId, ReportSyncErrorCliRequest request, CancellationToken ct = default)
    {
        var response = await client.ReportSyncErrorAsync(deviceId, sessionId, new ReportSyncErrorRequest
        {
            FilePath = request.FilePath,
            ErrorMessage = request.ErrorMessage,
            SongId = request.SongId,
            RecordId = request.RecordId
        }, ct);

        return SyncActionCounts.FromApi(response.Counts);
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
                Size = f.Size
            }).ToList(),
            Errors = result.Errors.Select(e => new ScanError
            {
                Path = e.Path,
                Error = e.Error
            }).ToList()
        };
    }
}
