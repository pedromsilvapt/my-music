using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Sources;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services;

public class ThumbnailProxyService(
    IDistributedCache cache,
    IOptions<ThumbnailCacheConfig> config,
    ILogger<ThumbnailProxyService> logger,
    IImageCacheService imageCacheService) : IThumbnailProxyService
{
    private readonly ThumbnailCacheConfig _config = config.Value;

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

        var base64Url = Base64UrlEncoder.Encode(originalUrl);
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
                logger.LogWarning(ex, "Failed to parse data URL: {Url}", url);
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
            logger.LogWarning(ex, "Failed to fetch image from: {Url}", url);
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
            originalUrl = Base64UrlEncoder.Decode(encodedUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decode URL: {EncodedUrl}", encodedUrl);
            return null;
        }

        if (originalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            IsProxyUrl(originalUrl))
        {
            return await GetImageAsync(originalUrl, cancellationToken);
        }

        var cacheKey = $"thumbnail:{encodedUrl}";
        var cachedResult = await imageCacheService.GetAsync(cacheKey, cancellationToken);

        if (cachedResult is not null)
        {
            var (mimeType, imageData) = cachedResult.Value;
            return ImageBuffer.FromBytes(imageData, ImageBuffer.ImageFormatFromMimeType(mimeType));
        }

        var imageBuffer = await GetImageAsync(originalUrl, cancellationToken);
        if (imageBuffer == null)
        {
            return null;
        }

        await imageCacheService.SetAsync(cacheKey, imageBuffer.MimeType, imageBuffer.Data, cancellationToken);

        return imageBuffer;
    }
}