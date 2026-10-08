namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface ISyncConfig
{
    /// <summary>
    /// Resolves the server device this installation syncs with. Devices are created in the web app: it
    /// fails when the server has none with the configured name.
    /// </summary>
    Task<long?> GetDeviceIdAsync(CancellationToken ct = default);
    string GetRepositoryPath();
    string[] GetMusicExtensions();
    string[] GetExcludePatterns();
    SyncChunkTuning GetChunkTuning();
    /// <summary>The modified date given to the files the sync downloads.</summary>
    FileModifiedAtSource GetFileModifiedAt();
    Task<int?> GetLastScanTotalAsync(CancellationToken ct = default);
    Task SetLastScanTotalAsync(int count, CancellationToken ct = default);
    Task SetLastSyncAtAsync(DateTime date, CancellationToken ct = default);
}