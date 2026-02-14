using AsyncKeyedLock;
using Microsoft.Extensions.Caching.Memory;

namespace MyMusic.Common.Sources;

public abstract class CachedSource : ISource
{
    private AsyncKeyedLocker<string> _asyncKeyedLocker = new();

    private IMemoryCache _searchesCache = new MemoryCache(new MemoryCacheOptions
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(30),
        SizeLimit = 10,
        CompactionPercentage = 0.7
    });

    private IMemoryCache _itemsCache = new MemoryCache(new MemoryCacheOptions
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(30),
        SizeLimit = 50,
        CompactionPercentage = 0.7
    });

    private Dictionary<string, SourcePurchasedSong> _activePurchases = new();

    private IMemoryCache _purchasesCache = new MemoryCache(new MemoryCacheOptions
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(30),
        SizeLimit = 1024 * 1024 * 1024, // 1 GB
        CompactionPercentage = 0.7
    });

    public Task<List<SourceSong>> SearchSongsAsync(string query, CancellationToken cancellationToken = default)
    {
        return GetCached(_searchesCache, $"searchSongs:{query}",
            () => SearchSongsInternalAsync(query, cancellationToken), cancellationToken);
    }

    public Task<SourceSong> GetSongAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetCached(_itemsCache, $"getSong:{id}", () => GetSongInternalAsync(id, cancellationToken),
            cancellationToken);
    }

    public async Task<Stream> PurchaseSongAsync(string id, CancellationToken cancellationToken = default)
    {
        using (await _asyncKeyedLocker.LockAsync($"purchaseSong:{id}", cancellationToken))
        {
            return await PurchaseSongInternalAsync(id, new Progress<double>(), cancellationToken);
        }
    }

    public Task<SourcePurchasedSong>
        PurchaseSongStatusAsync(string id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Stream> DownloadSongAsync(string id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<List<SourceAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default)
    {
        return GetCached(_searchesCache, $"searchAlbums:{query}",
            () => SearchAlbumsInternalAsync(query, cancellationToken), cancellationToken);
    }

    #region Private Methods

    private async Task<T> GetCached<T>(IMemoryCache memoryCache, string key, Func<Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        using (await _asyncKeyedLocker.LockAsync(key, cancellationToken))
        {
            if (!memoryCache.TryGetValue<T>(key, out var result) || result is null)
            {
                result = await factory();

                memoryCache.Set<T>(key, result, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                });
            }

            return result;
        }
    }

    #endregion Private Methods

    #region Abstract Methods

    protected abstract Task<List<SourceSong>> SearchSongsInternalAsync(string query,
        CancellationToken cancellationToken = default);

    protected abstract Task<SourceSong> GetSongInternalAsync(string id, CancellationToken cancellationToken = default);

    protected abstract Task<Stream> PurchaseSongInternalAsync(string id, IProgress<double> progress,
        CancellationToken cancellationToken = default);

    protected abstract Task<List<SourceAlbum>> SearchAlbumsInternalAsync(string query,
        CancellationToken cancellationToken = default);

    #endregion Abstract Methods
}