namespace MyMusic.CLI.Services.Sync;

public interface IFileOps
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    Task EnsureDirectoryAsync(string path, CancellationToken ct = default);
    Task WriteFileAsync(string path, Stream content, CancellationToken ct = default);
    Task DeleteFileAsync(string path, CancellationToken ct = default);
    Task MoveFileAsync(string fromPath, string toPath, CancellationToken ct = default);
    /// <summary>
    /// Computes the base64 checksum of a file with the named algorithm, the same way the server does.
    /// Throws <see cref="NotSupportedException"/> for an algorithm this client does not implement.
    /// </summary>
    Task<string> ComputeChecksumAsync(string path, string algorithm, CancellationToken ct = default);
    Task<DateTime?> GetModificationTimeAsync(string path, CancellationToken ct = default);
    void CleanupEmptyParentDirectories(string filePath, string repositoryRoot);
}