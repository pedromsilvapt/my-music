using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
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

    [Fact]
    public async Task Save_WithResolveConflict_ShouldUseResolvedPath()
    {
        var fs = new MockFileSystem();
        const string resolvedPath = "/data/Artist/New Album/New Song - Artist (2).mp3";

        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(FilePath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "New Song")
        {
            Album = new AlbumMetadata(null, "New Album", new CoverArtMetadata(), new ArtistMetadata(null, "Artist")),
            Artists = [new ArtistMetadata(null, "Artist")],
        };

        var target = new FileTarget(fs)
        {
            Folder = "/data",
        };

        var naming = new NamingMetadata { Extension = ".mp3" };

        await using (var sourceStream = fs.FileStream.New(FilePath, FileMode.Open, FileAccess.Read))
        {
            await target.Save(sourceStream, metadata, naming, newPath =>
            {
                newPath.ShouldContain("New Song - Artist");
                return Task.FromResult(resolvedPath);
            });
        }

        target.FilePath.ShouldBe(resolvedPath);
        fs.File.Exists(resolvedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Save_WithResolveConflict_ReturningSamePath_ShouldUseThatPath()
    {
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(FilePath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "Test Song")
        {
            Album = new AlbumMetadata(null, "Test Album", new CoverArtMetadata(), new ArtistMetadata(null, "Test Artist")),
            Artists = [new ArtistMetadata(null, "Test Artist")],
        };

        var target = new FileTarget(fs)
        {
            Folder = "/data",
        };

        var naming = new NamingMetadata { Extension = ".mp3" };
        var expectedPath = "/data/Test Artist/Test Album/Test Song - Test Artist.mp3";

        await using (var sourceStream = fs.FileStream.New(FilePath, FileMode.Open, FileAccess.Read))
        {
            await target.Save(sourceStream, metadata, naming, newPath => Task.FromResult(newPath));
        }

        target.FilePath.ShouldBe(expectedPath);
        fs.File.Exists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Save_WithoutResolveConflict_ShouldBehaveAsBefore()
    {
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(FilePath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "Test Song")
        {
            Album = new AlbumMetadata(null, "Test Album", new CoverArtMetadata(), new ArtistMetadata(null, "Test Artist")),
            Artists = [new ArtistMetadata(null, "Test Artist")],
        };

        var target = new FileTarget(fs)
        {
            Folder = "/data",
        };

        var naming = new NamingMetadata { Extension = ".mp3" };
        var expectedPath = "/data/Test Artist/Test Album/Test Song - Test Artist.mp3";

        await using (var sourceStream = fs.FileStream.New(FilePath, FileMode.Open, FileAccess.Read))
        {
            await target.Save(sourceStream, metadata, naming);
        }

        target.FilePath.ShouldBe(expectedPath);
        fs.File.Exists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Relocate_WithResolveConflict_ShouldUseResolvedPath()
    {
        var fs = new MockFileSystem();
        const string originalPath = "/data/artist/album/old-song.mp3";
        const string resolvedPath = "/data/artist/album/Artist/New Album/New Song - Artist (2).mp3";

        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(originalPath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "New Song")
        {
            Album = new AlbumMetadata(null, "New Album", new CoverArtMetadata(), new ArtistMetadata(null, "Artist")),
            Artists = [new ArtistMetadata(null, "Artist")],
        };

        var target = new FileTarget(fs)
        {
            FilePath = originalPath,
            Folder = "/data",
        };

        await target.SaveMetadata(metadata);

        var naming = NamingMetadata.FromPath(originalPath);
        await target.Relocate(naming, newPath =>
        {
            newPath.ShouldContain("New Song - Artist");
            return Task.FromResult(resolvedPath);
        });

        target.FilePath.ShouldBe(resolvedPath);
        fs.File.Exists(resolvedPath).ShouldBeTrue();
        fs.File.Exists(originalPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Relocate_WithResolveConflict_ReturningSamePath_ShouldNotMove()
    {
        var fs = new MockFileSystem();
        const string originalPath = "/data/artist/album/old-song.mp3";

        fs.Directory.CreateDirectory("/data/artist/album");
        fs.File.WriteAllBytes(originalPath, MockMusicFile.GetTestMusicFile());

        var metadata = new SongMetadata(null, "New Song")
        {
            Album = new AlbumMetadata(null, "New Album", new CoverArtMetadata(), new ArtistMetadata(null, "Artist")),
            Artists = [new ArtistMetadata(null, "Artist")],
        };

        var target = new FileTarget(fs)
        {
            FilePath = originalPath,
            Folder = "/data",
        };

        await target.SaveMetadata(metadata);

        var naming = NamingMetadata.FromPath(originalPath);
        await target.Relocate(naming, newPath => Task.FromResult(originalPath));

        target.FilePath.ShouldBe(originalPath);
        fs.File.Exists(originalPath).ShouldBeTrue();
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
