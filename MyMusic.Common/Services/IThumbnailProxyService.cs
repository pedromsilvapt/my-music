using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

public interface IThumbnailProxyService
{
    string GetProxyUrl(string originalUrl);

    SourceArtwork? TransformArtwork(SourceArtwork? artwork);

    Task<ImageBuffer?> GetProxyImageAsync(string encodedUrl, CancellationToken cancellationToken = default);

    bool IsProxyUrl(string url);

    Task<ImageBuffer?> GetImageAsync(string url, CancellationToken cancellationToken = default);
}