namespace MyMusic.Common.Services.Sync;

public interface IFileOps
{
    bool FileExists(string path);
    Task EnsureDirectoryAsync(string path, CancellationToken ct = default);
    Task WriteFileAsync(string path, Stream content, CancellationToken ct = default);
    Task DeleteFileAsync(string path, CancellationToken ct = default);
    Task<string> ReadFileBase64Async(string path, CancellationToken ct = default);
    Task<DateTime?> GetModificationTimeAsync(string path, CancellationToken ct = default);
}
