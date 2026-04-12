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
        var song = CreateSong(scenario, "Artist/Song.mp3", bitrate: 320);

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
        var song = CreateSong(scenario, "Artist/Song.mp3", bitrate: null);

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
        var song = CreateSong(scenario, "Artist/Missing.mp3", bitrate: null);

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
        var songWithBitrate = CreateSong(scenario, "Artist/HasBitrate.mp3", bitrate: 256);
        var songWithoutBitrate = CreateSong(scenario, "Artist/Backfill.mp3", bitrate: null);

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

    private static Song CreateSong(Scenario scenario, string repositoryPath, int? bitrate)
    {
        var artist = new Artist
        {
            Name = $"Test Artist {Guid.NewGuid()}",
            OwnerId = scenario.AdminUser.Id,
            Owner = scenario.AdminUser,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        scenario.DbContext.Artists.Add(artist);
        scenario.DbContext.SaveChanges();

        var album = new Album
        {
            Name = $"Test Album {Guid.NewGuid()}",
            OwnerId = scenario.AdminUser.Id,
            Owner = scenario.AdminUser,
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        scenario.DbContext.Albums.Add(album);
        scenario.DbContext.SaveChanges();

        var song = new Song
        {
            Title = "Test Song",
            Label = "Test Song",
            Album = album,
            AlbumId = album.Id,
            Owner = scenario.AdminUser,
            OwnerId = scenario.AdminUser.Id,
            RepositoryPath = repositoryPath,
            Checksum = "abc123",
            ChecksumAlgorithm = "SHA256",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Bitrate = bitrate,
            Duration = TimeSpan.FromSeconds(180),
            Size = 1000,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };

        scenario.DbContext.Songs.Add(song);
        scenario.DbContext.SaveChanges();

        return song;
    }
}
