using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace MyMusic.Common.Services;

public interface IImageCacheService
{
    Task<(string mimeType, byte[] imageData)?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync(string cacheKey, string mimeType, byte[] imageData, CancellationToken cancellationToken = default);
}

public class ImageCacheService(
    IDistributedCache cache,
    IOptions<ThumbnailCacheConfig> config,
    ILogger<ImageCacheService> logger) : IImageCacheService
{
    private readonly ThumbnailCacheConfig _config = config.Value;

    public async Task<(string mimeType, byte[] imageData)?> GetAsync(
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        var cachedData = await cache.GetAsync(cacheKey, cancellationToken);

        if (cachedData is null || cachedData.Length <= 1)
        {
            return null;
        }

        logger.LogDebug("Cache hit for thumbnail: {CacheKey}", cacheKey);
        var (mimeType, imageData) = DecodeFromCache(cachedData);
        return (mimeType, imageData);
    }

    public async Task SetAsync(
        string cacheKey,
        string mimeType,
        byte[] imageData,
        CancellationToken cancellationToken = default)
    {
        if (imageData.Length > _config.MaxEntrySizeBytes)
        {
            logger.LogDebug("Skipped caching thumbnail {CacheKey} - exceeds size limit ({Size} > {Max})",
                cacheKey, imageData.Length, _config.MaxEntrySizeBytes);
            return;
        }

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_config.EntryTtlMinutes)
        };

        var cacheData = EncodeForCache(mimeType, imageData);
        await cache.SetAsync(cacheKey, cacheData, cacheOptions, cancellationToken);
        logger.LogDebug("Cached thumbnail: {CacheKey}, size: {Size} bytes", cacheKey, imageData.Length);
    }

    private static byte[] EncodeForCache(string mimeType, byte[] imageData)
    {
        var mimeBytes = Encoding.UTF8.GetBytes(mimeType);
        var result = new byte[1 + mimeBytes.Length + imageData.Length];
        result[0] = (byte)mimeBytes.Length;
        Buffer.BlockCopy(mimeBytes, 0, result, 1, mimeBytes.Length);
        Buffer.BlockCopy(imageData, 0, result, 1 + mimeBytes.Length, imageData.Length);
        return result;
    }

    private static (string mimeType, byte[] imageData) DecodeFromCache(byte[] cachedData)
    {
        var mimeLength = cachedData[0];
        var mimeBytes = new byte[mimeLength];
        Buffer.BlockCopy(cachedData, 1, mimeBytes, 0, mimeLength);
        var mimeType = Encoding.UTF8.GetString(mimeBytes);
        var imageData = new byte[cachedData.Length - 1 - mimeLength];
        Buffer.BlockCopy(cachedData, 1 + mimeLength, imageData, 0, imageData.Length);
        return (mimeType, imageData);
    }
}
