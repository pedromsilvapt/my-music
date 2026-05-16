using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using MyMusic.Common.Metadata;
using MyMusic.Common.Targets;
using MyMusic.Common.Tests.Utilities;
using Shouldly;

namespace MyMusic.Common.Tests.Targets;

public class FileTargetSpecs
{
    private const string FilePath = "/data/artist/album/test-song.mp3";

    [Fact]
    public async Task SaveMetadata_BigArtworkThenSmallArtwork_FileSizeShouldDecrease()
    {
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(FilePath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "Test Song")
        {
            Album = new AlbumMetadata(null, "Test Album", CreateCoverArt(2500, 2500), new ArtistMetadata(null, "Test Artist")),
            Artists = [new ArtistMetadata(null, "Test Artist")],
            Genres = ["Rock"],
            Year = 2023,
        };

        var target = new FileTarget(fs) { FilePath = FilePath };

        // Save with big artwork (1 MB+)
        await target.SaveMetadata(metadata);
        var bigFileSize = fs.FileInfo.New(FilePath).Length;
        bigFileSize.ShouldBeGreaterThan(1_000_000);

        // Save with small artwork over the same file
        metadata.Album!.CoverArt = CreateCoverArt(500, 500);
        await target.SaveMetadata(metadata);
        var smallFileSize = fs.FileInfo.New(FilePath).Length;

        // File with small artwork should be smaller than file with big artwork
        smallFileSize.ShouldBeLessThan(bigFileSize);
    }

    [Fact]
    public async Task SaveMetadata_SmallArtworkThenBigArtwork_FileSizeShouldIncrease()
    {
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(FilePath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "Test Song")
        {
            Album = new AlbumMetadata(null, "Test Album", CreateCoverArt(500, 500), new ArtistMetadata(null, "Test Artist")),
            Artists = [new ArtistMetadata(null, "Test Artist")],
            Genres = ["Rock"],
            Year = 2023,
        };

        var target = new FileTarget(fs) { FilePath = FilePath };

        // Save with small artwork
        await target.SaveMetadata(metadata);
        var smallFileSize = fs.FileInfo.New(FilePath).Length;
        smallFileSize.ShouldBeLessThan(1_000_000);

        // Save with big artwork (1 MB+) over the same file
        metadata.Album!.CoverArt = CreateCoverArt(2500, 2500);
        await target.SaveMetadata(metadata);
        var bigFileSize = fs.FileInfo.New(FilePath).Length;

        // File with small artwork should be smaller than file with big artwork
        bigFileSize.ShouldBeGreaterThan(smallFileSize);
    }

    private static CoverArtMetadata CreateCoverArt(int width, int height)
    {
        return new CoverArtMetadata(small: null, normal: null, big: CreateJpegImage(width, height));
    }

    private static string CreateJpegImage(int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var random = new Random(42);

        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        var rowBytes = width * 3;
        var padding = bitmapData.Stride - rowBytes;
        var pixelBuffer = new byte[rowBytes + padding];

        for (var y = 0; y < height; y++)
        {
            random.NextBytes(pixelBuffer);
            Marshal.Copy(pixelBuffer, 0, bitmapData.Scan0 + y * bitmapData.Stride, bitmapData.Stride);
        }

        bitmap.UnlockBits(bitmapData);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);

        return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
    }
}
