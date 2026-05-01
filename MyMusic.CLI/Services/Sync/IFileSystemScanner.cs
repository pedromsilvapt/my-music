namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface IFileSystemScanner
{
    Task<ScanResult> ScanAsync(
        string rootPath,
        string[] extensions,
        string[] excludePatterns,
        Action<int, string>? onProgress = null,
        Action<string, string>? onError = null,
        CancellationToken ct = default);
}
