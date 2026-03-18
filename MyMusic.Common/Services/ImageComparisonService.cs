using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services;

public class ImageComparisonService(
    IThumbnailProxyService thumbnailProxyService,
    ILogger<ImageComparisonService> logger) : IImageComparisonService
{

    public async Task<bool> AreImagesDifferentAsync(
        byte[] localImageData,
        string remoteImageUrl,
        CancellationToken cancellationToken)
    {
        if (localImageData.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(remoteImageUrl))
        {
            return true;
        }

        try
        {
            var remoteImage = await thumbnailProxyService.GetImageAsync(remoteImageUrl, cancellationToken);

            if (remoteImage is null)
            {
                logger.LogWarning("Failed to fetch remote image for comparison: {Url}", remoteImageUrl);
                return true;
            }

            return !localImageData.SequenceEqual(remoteImage.Data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error comparing images - URL: {Url}", remoteImageUrl);
            return true;
        }
    }

    public bool AreImagesIdentical(byte[] imageData1, byte[] imageData2)
    {
        if (imageData1 == null || imageData2 == null)
        {
            return imageData1 == imageData2;
        }

        return imageData1.SequenceEqual(imageData2);
    }
}