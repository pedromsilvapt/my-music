namespace MyMusic.Common.Services.Sync;

public interface ISyncConfig
{
    Task<long?> GetDeviceIdAsync(CancellationToken ct = default);
    string GetRepositoryPath();
    string[] GetMusicExtensions();
    string[] GetExcludePatterns();
    int GetChunkSize();
    Task<int?> GetLastScanTotalAsync(CancellationToken ct = default);
    Task SetLastScanTotalAsync(int count, CancellationToken ct = default);
    Task SetLastSyncAtAsync(DateTime date, CancellationToken ct = default);
}
