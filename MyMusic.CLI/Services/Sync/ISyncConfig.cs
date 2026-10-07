namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface ISyncConfig
{
    /// <summary>
    /// Resolves the server device for this installation, registering it when missing.
    /// <paramref name="saveOptions"/> also saves the configured device options to an existing device.
    /// </summary>
    Task<long?> GetDeviceIdAsync(bool saveOptions, CancellationToken ct = default);
    string? GetNamingTemplate();
    string GetRepositoryPath();
    string[] GetMusicExtensions();
    string[] GetExcludePatterns();
    SyncChunkTuning GetChunkTuning();
    Task<int?> GetLastScanTotalAsync(CancellationToken ct = default);
    Task SetLastScanTotalAsync(int count, CancellationToken ct = default);
    Task SetLastSyncAtAsync(DateTime date, CancellationToken ct = default);
}