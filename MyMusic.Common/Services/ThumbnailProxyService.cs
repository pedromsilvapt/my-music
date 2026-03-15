using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

public class ThumbnailProxyService(
    IDistributedCache cache,
    IOptions<ThumbnailCacheConfig> config,
    ILogger<ThumbnailProxyService> logger) : IThumbnailProxyService
{
    private readonly ThumbnailCacheConfig _config = config.Value;
    private readonly ILogger<ThumbnailProxyService> _logger = logger;

    public string GetProxyUrl(string originalUrl)
    {
        if (string.IsNullOrEmpty(originalUrl))
        {
            return originalUrl;
        }

        if (originalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        if (originalUrl.StartsWith(_config.ProxyPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        var base64Url = EncodeBase64Url(originalUrl);
        return _config.ProxyPathPrefix + base64Url;
    }

    public SourceArtwork? TransformArtwork(SourceArtwork? artwork)
    {
        if (artwork is null)
        {
            return null;
        }

        return new SourceArtwork(
            small: TransformUrl(artwork.Small),
            normal: TransformUrl(artwork.Normal),
            big: TransformUrl(artwork.Big)
        );
    }

    private string? TransformUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        return GetProxyUrl(url);
    }

    public bool IsProxyUrl(string url)
    {
        return !string.IsNullOrEmpty(url) &&
               url.StartsWith(_config.ProxyPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ImageBuffer?> GetImageAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return ImageBuffer.FromBase64Url(url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse data URL: {Url}", url);
                return null;
            }
        }

        if (IsProxyUrl(url))
        {
            var encodedUrl = url[_config.ProxyPathPrefix.Length..];
            return await GetProxyImageAsync(encodedUrl, cancellationToken);
        }

        try
        {
            return await ImageBuffer.FromWebUrlAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch image from: {Url}", url);
            return null;
        }
    }

    public async Task<ImageBuffer?> GetProxyImageAsync(
        string encodedUrl,
        CancellationToken cancellationToken = default)
    {
        string originalUrl;
        try
        {
            originalUrl = DecodeBase64Url(encodedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode URL: {EncodedUrl}", encodedUrl);
            return null;
        }

        if (originalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            IsProxyUrl(originalUrl))
        {
            return await GetImageAsync(originalUrl, cancellationToken);
        }

        var cacheKey = $"thumbnail:{encodedUrl}";
        var cachedData = await cache.GetAsync(cacheKey, cancellationToken);

        if (cachedData is not null && cachedData.Length > 1)
        {
            _logger.LogDebug("Cache hit for thumbnail: {EncodedUrl}", encodedUrl);
            var (mimeType, imageData) = DecodeFromCache(cachedData);
            return ImageBuffer.FromBytes(imageData, ImageBuffer.ImageFormatFromMimeType(mimeType));
        }

        var imageBuffer = await GetImageAsync(originalUrl, cancellationToken);
        if (imageBuffer == null)
        {
            return null;
        }

        if (imageBuffer.Data.Length <= _config.MaxEntrySizeBytes)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_config.EntryTtlMinutes)
            };

            var cacheData = EncodeForCache(imageBuffer.MimeType, imageBuffer.Data);
            await cache.SetAsync(cacheKey, cacheData, cacheOptions, cancellationToken);
            _logger.LogDebug("Cached thumbnail: {EncodedUrl}, size: {Size} bytes", encodedUrl, imageBuffer.Data.Length);
        }
        else
        {
            _logger.LogDebug("Skipped caching thumbnail {EncodedUrl} - exceeds size limit ({Size} > {Max})",
                encodedUrl, imageBuffer.Data.Length, _config.MaxEntrySizeBytes);
        }

        return imageBuffer;
    }

    private static string EncodeBase64Url(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string DecodeBase64Url(string input)
    {
        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
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