using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class BitrateBackfillServiceSpecs
{
    [Fact]
    public async Task BackfillBatch_NoNullBitrateSongs_ReturnsZero()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Test Song", repositoryPath: "Artist/Song.mp3", bitrate: 320, checksum: "abc123", checksumAlgorithm: "SHA256");

        // Act
        var (processed, errors) = await BitrateBackfillService.BackfillBatchAsync(
            scenario.DbContext,
            "/data",
            scenario.FileSystem,
            Substitute.For<ILogger<BitrateBackfillService>>(),
            CancellationToken.None);

        // Assert
        processed.ShouldBe(0);
        errors.ShouldBe(0);
        song.Bitrate.ShouldBe(320);
    }

    [Fact]
    public async Task BackfillBatch_NullBitrateSongWithFile_UpdatesBitrate()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Test Song", repositoryPath: "Artist/Song.mp3", bitrate: null, checksum: "abc123", checksumAlgorithm: "SHA256");

        MockMusicFile.Create(scenario.FileSystem, "/data/Artist/Song.mp3", "Song", "Album", ["Artist"], ["Genre"]);

        // Act
        var (processed, errors) = await BitrateBackfillService.BackfillBatchAsync(
            scenario.DbContext,
            "/data",
            scenario.FileSystem,
            Substitute.For<ILogger<BitrateBackfillService>>(),
            CancellationToken.None);

        // Assert
        processed.ShouldBe(1);
        errors.ShouldBe(0);

        scenario.DbContext.Songs.First(s => s.Id == song.Id).Bitrate.ShouldNotBeNull();
        scenario.DbContext.Songs.First(s => s.Id == song.Id).Bitrate!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task BackfillBatch_NullBitrateSongWithoutFile_CountsAsError()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Test Song", repositoryPath: "Artist/Missing.mp3", bitrate: null, checksum: "abc123", checksumAlgorithm: "SHA256");

        // Act
        var (processed, errors) = await BitrateBackfillService.BackfillBatchAsync(
            scenario.DbContext,
            "/data",
            scenario.FileSystem,
            Substitute.For<ILogger<BitrateBackfillService>>(),
            CancellationToken.None);

        // Assert
        processed.ShouldBe(1);
        errors.ShouldBe(1);

        scenario.DbContext.Songs.First(s => s.Id == song.Id).Bitrate.ShouldBeNull();
    }

    [Fact]
    public async Task BackfillBatch_MixedSongs_UpdatesOnlyNullBitrate()
    {
        // Arrange
        var scenario = new Scenario();
        var songWithBitrate = scenario.CreateSong("Test Song", repositoryPath: "Artist/HasBitrate.mp3", bitrate: 256, checksum: "abc123", checksumAlgorithm: "SHA256");
        var songWithoutBitrate = scenario.CreateSong("Test Song", repositoryPath: "Artist/Backfill.mp3", bitrate: null, checksum: "abc123", checksumAlgorithm: "SHA256");

        MockMusicFile.Create(scenario.FileSystem, "/data/Artist/Backfill.mp3", "Backfill", "Album", ["Artist"], ["Genre"]);

        // Act
        var (processed, errors) = await BitrateBackfillService.BackfillBatchAsync(
            scenario.DbContext,
            "/data",
            scenario.FileSystem,
            Substitute.For<ILogger<BitrateBackfillService>>(),
            CancellationToken.None);

        // Assert
        processed.ShouldBe(1);
        errors.ShouldBe(0);

        scenario.DbContext.Songs.First(s => s.Id == songWithBitrate.Id).Bitrate.ShouldBe(256);
        scenario.DbContext.Songs.First(s => s.Id == songWithoutBitrate.Id).Bitrate.ShouldNotBeNull();
    }

}
