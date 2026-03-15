namespace MyMusic.Common.Services;

public interface IImageComparisonService
{
    Task<bool> AreImagesDifferentAsync(byte[] localImageData, string remoteImageUrl, CancellationToken cancellationToken);

    bool AreImagesIdentical(byte[] imageData1, byte[] imageData2);
}