using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services.SongHistory;

public class SongHistoryThumbnailService(ILogger<SongHistoryThumbnailService> logger)
    : ISongHistoryThumbnailService
{
    /// <summary>Max thumbnail dimension (width or height) in pixels.</summary>
    public const int MaxThumbnailSize = 265;

    /// <inheritdoc />
    public byte[]? GenerateThumbnail(string base64Data, string mimeType)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Data);
            var image = ImageBuffer.FromBytes(bytes, mimeType);

            // Resize so the largest side is MaxThumbnailSize, preserving aspect ratio.
            // Only the width-based branch of ToResized uses floating-point aspect
            // ratio math, so always go through it: for portrait images compute
            // the target width that makes the height equal MaxThumbnailSize.
            var (width, height) = (image.Size.Width, image.Size.Height);
            var targetWidth = width >= height
                ? MaxThumbnailSize
                : (int)(MaxThumbnailSize * ((double)width / height));

            var resized = image.ToResized(targetWidth);

            return resized.Data;
        }
        catch (Exception ex)
        {
            // Graceful degradation: thumbnail generation is best-effort.
            // The history entry is still written without a thumbnail.
            logger.LogWarning(ex, "Failed to generate song history thumbnail");
            return null;
        }
    }
}