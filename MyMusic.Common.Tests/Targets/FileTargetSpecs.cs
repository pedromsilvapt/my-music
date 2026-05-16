using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using MyMusic.Common.Metadata;
using MyMusic.Common.Services;
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

    [Fact]
    public async Task SaveMetadata_WithSameMetadataChangeOnTwoIdenticalFiles_ChecksumsShouldMatch()
    {
        // Arrange - Create two identical MP3 files
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/data/artist/album");

        const string fileA = "/data/artist/album/song-a.mp3";
        const string fileB = "/data/artist/album/song-b.mp3";

        fs.File.WriteAllBytes(fileA, MockMusicFile.GetTestMusicFile());
        fs.File.WriteAllBytes(fileB, MockMusicFile.GetTestMusicFile());

        // Write initial metadata with different titles to simulate different starting states
        // File A starts with title "the alibi" (lowercase)
        var metadataA = new SongMetadata(null, "the alibi")
        {
            Album = new AlbumMetadata(null, "The Alibi", new CoverArtMetadata(), new ArtistMetadata(null, "Dylan")),
            Artists = [new ArtistMetadata(null, "Dylan")],
            Genres = ["Rock"],
        };

        // File B starts with title "The Alibi" (proper casing)
        var metadataB = new SongMetadata(null, "The Alibi")
        {
            Album = new AlbumMetadata(null, "The Alibi", new CoverArtMetadata(), new ArtistMetadata(null, "Dylan")),
            Artists = [new ArtistMetadata(null, "Dylan")],
            Genres = ["Rock"],
        };

        // Write initial metadata to both files
        var targetA = new FileTarget(fs) { FilePath = fileA };
        var targetB = new FileTarget(fs) { FilePath = fileB };

        await targetA.SaveMetadata(metadataA);
        await targetB.SaveMetadata(metadataB);

        // Get initial checksums - they should differ because titles are different
        var initialChecksumA = ChecksumService.CalculateChecksum(new XxHash128(), fs.File.ReadAllBytes(fileA));
        var initialChecksumB = ChecksumService.CalculateChecksum(new XxHash128(), fs.File.ReadAllBytes(fileB));
        initialChecksumA.ShouldNotBe(initialChecksumB, "Initial checksums should differ because titles are different");

        // Act - Now update file A to have the same metadata as file B
        // This simulates the CLI test scenario: updating title from "the alibi" to "The Alibi"
        var targetAUpdated = new FileTarget(fs) { FilePath = fileA };
        await targetAUpdated.SaveMetadata(metadataB);

        // Get final checksums
        var finalChecksumA = ChecksumService.CalculateChecksum(new XxHash128(), fs.File.ReadAllBytes(fileA));
        var finalChecksumB = ChecksumService.CalculateChecksum(new XxHash128(), fs.File.ReadAllBytes(fileB));

        // Assert - Both files should have identical checksums after the update
        finalChecksumA.ShouldBe(finalChecksumB,
            "Checksums should match when both files have identical metadata. " +
            $"File A: {initialChecksumA} -> {finalChecksumA}, File B: {initialChecksumB} -> {finalChecksumB}");
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
