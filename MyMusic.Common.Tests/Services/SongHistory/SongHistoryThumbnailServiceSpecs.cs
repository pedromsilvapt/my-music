using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services.SongHistory;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryThumbnailServiceSpecs
{
    private readonly SongHistoryThumbnailService _service = new(
        Substitute.For<ILogger<SongHistoryThumbnailService>>());

    /// <summary>
    /// Encodes a programmatically-generated bitmap of the given dimensions
    /// to the specified format and returns its base64 representation plus
    /// the corresponding mime type.
    /// </summary>
    private static (string base64, string mimeType) MakeImage(
        int width, int height, ImageFormat format, string mimeType)
    {
        using var bitmap = new Bitmap(width, height);
        // Fill with a solid color so the bitmap has real pixel data.
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(200, 120, 60));
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, format);
        return (Convert.ToBase64String(stream.ToArray()), mimeType);
    }

    private static (int width, int height) GetImageDimensions(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        using var image = Image.FromStream(ms);
        return (image.Width, image.Height);
    }

    [Fact]
    public void GenerateThumbnail_ValidJpeg_ReturnsResizedBytes()
    {
        // Arrange - a small JPEG larger than the thumbnail max
        var (base64, mimeType) = MakeImage(500, 500, ImageFormat.Jpeg, "image/jpeg");

        // Act
        var result = _service.GenerateThumbnail(base64, mimeType);

        // Assert
        result.ShouldNotBeNull();
        var (w, h) = GetImageDimensions(result!);
        w.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);
        h.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);
    }

    [Fact]
    public void GenerateThumbnail_LandscapeImage_PreservesAspectRatio()
    {
        // Arrange - a wide 800x200 image
        var (base64, mimeType) = MakeImage(800, 200, ImageFormat.Jpeg, "image/jpeg");

        // Act
        var result = _service.GenerateThumbnail(base64, mimeType);

        // Assert - width should hit the max, height should preserve aspect ratio
        result.ShouldNotBeNull();
        var (w, h) = GetImageDimensions(result!);
        w.ShouldBe(SongHistoryThumbnailService.MaxThumbnailSize);
        // 800:200 = 4:1, so height should be 265/4 ≈ 66
        h.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);
        // Aspect ratio preserved (within rounding)
        var originalRatio = 800.0 / 200.0;
        var resultRatio = (double)w / h;
        resultRatio.ShouldBe(originalRatio, 0.05);
    }

    [Fact]
    public void GenerateThumbnail_SquareImage_Returns265x265()
    {
        // Arrange - a 500x500 square image
        var (base64, mimeType) = MakeImage(500, 500, ImageFormat.Jpeg, "image/jpeg");

        // Act
        var result = _service.GenerateThumbnail(base64, mimeType);

        // Assert
        result.ShouldNotBeNull();
        var (w, h) = GetImageDimensions(result!);
        w.ShouldBe(SongHistoryThumbnailService.MaxThumbnailSize);
        h.ShouldBe(SongHistoryThumbnailService.MaxThumbnailSize);
    }

    [Fact]
    public void GenerateThumbnail_PortraitImage_PreservesAspectRatio()
    {
        // Arrange - a tall 200x800 portrait image
        var (base64, mimeType) = MakeImage(200, 800, ImageFormat.Jpeg, "image/jpeg");

        // Act
        var result = _service.GenerateThumbnail(base64, mimeType);

        // Assert - height should hit (or be within 1px of) the max, and the
        // aspect ratio should be preserved. The existing ToResized uses
        // integer truncation in its aspect-ratio math, so a portrait image
        // may land at 264 instead of 265 — both satisfy the "max 265" spec.
        result.ShouldNotBeNull();
        var (w, h) = GetImageDimensions(result!);
        h.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);
        h.ShouldBeGreaterThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize - 1);
        w.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);
        // Aspect ratio preserved (within rounding)
        var originalRatio = 200.0 / 800.0;
        var resultRatio = (double)w / h;
        resultRatio.ShouldBe(originalRatio, 0.05);
    }

    [Fact]
    public void GenerateThumbnail_InvalidData_ReturnsNull()
    {
        // Arrange - not valid base64 image content
        var invalidBase64 = Convert.ToBase64String("not-an-image"u8.ToArray());

        // Act
        var result = _service.GenerateThumbnail(invalidBase64, "image/jpeg");

        // Assert - graceful degradation, no exception thrown
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateThumbnail_InvalidMimeType_ReturnsNull()
    {
        // Arrange - valid image bytes but an unsupported mime type
        var (base64, _) = MakeImage(100, 100, ImageFormat.Jpeg, "image/jpeg");

        // Act
        var result = _service.GenerateThumbnail(base64, "image/notarealformat");

        // Assert - graceful degradation, no exception thrown
        result.ShouldBeNull();
    }

}