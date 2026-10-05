using System.IO.Abstractions;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Sync;

namespace MyMusic.CLI.Services;

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(
        string rootPath,
        RepositoryOptions options,
        Action<int, string>? onProgress = null,
        Action<string, string>? onError = null,
        CancellationToken ct = default);
}

public record FileMetadata(string RelativePath, string FullPath, DateTime ModifiedAt, DateTime CreatedAt, long Size = 0);

public record FileScanResult
{
    public required List<FileMetadata> Files { get; init; }
    public required List<ScanErrorRecord> Errors { get; init; }
}

public record ScanErrorRecord
{
    public required string Path { get; init; }
    public required string Error { get; init; }
}

public class FileScanner(IFileSystem fileSystem) : IFileScanner
{
    public Task<FileScanResult> ScanAsync(
        string rootPath,
        RepositoryOptions options,
        Action<int, string>? onProgress = null,
        Action<string, string>? onError = null,
        CancellationToken ct = default)
    {
        var files = new List<FileMetadata>();
        var errors = new List<ScanErrorRecord>();
        var scannedCount = 0;

        if (!fileSystem.Directory.Exists(rootPath))
        {
            return Task.FromResult(new FileScanResult { Files = files, Errors = errors });
        }

        var exclusions = new ExclusionMatcher(options.ExcludePatterns);
        var allFiles = fileSystem.Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);

        foreach (var filePath in allFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
            var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";

            if (exclusions.Match(relativePath) != null)
            {
                continue;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!options.MusicExtensions.Contains(extension))
            {
                continue;
            }

            try
            {
                var fileInfo = fileSystem.FileInfo.New(filePath);
                files.Add(new FileMetadata(relativePath, filePath.Replace('\\', '/'), fileInfo.LastWriteTimeUtc,
                    fileInfo.CreationTimeUtc, fileInfo.Length));
                scannedCount++;

                if (onProgress != null && scannedCount % 10 == 0)
                {
                    onProgress(scannedCount, directory);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to read file info: {ex.Message}";
                errors.Add(new ScanErrorRecord { Path = relativePath, Error = errorMsg });
                onError?.Invoke(relativePath, errorMsg);
            }
        }

        onProgress?.Invoke(scannedCount, rootPath);

        return Task.FromResult(new FileScanResult { Files = files, Errors = errors });
    }
}
