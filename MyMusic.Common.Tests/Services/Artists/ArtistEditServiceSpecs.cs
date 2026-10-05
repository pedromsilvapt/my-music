using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Artists;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Artists;

public class ArtistEditServiceSpecs
{
    private static ArtistEditService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(songFileUpdate),
            Substitute.For<ILogger<ArtistEditService>>());

    private static ArtistEditInput Edit(Artist artist, string name) => new() { ArtistId = artist.Id, Name = name };

    [Fact]
    public async Task EditAsync_RenamedArtist_KeepsItsRowAndRewritesTheSongsOfItsAlbumsAndTheOnesItIsFeaturedIn()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var other = scenario.CreateArtist("Other");
        var album = scenario.CreateAlbum("Album", artist);
        var othersAlbum = scenario.CreateAlbum("Their Album", other);
        var device = scenario.CreateDevice("Phone");
        var own = await scenario.CreateSyncedSongAsync("Own", album, device);
        var featured = await scenario.CreateSyncedSongAsync("Featured", othersAlbum, device, [other, artist]);
        var unrelated = await scenario.CreateSyncedSongAsync("Unrelated", othersAlbum, device);

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, " Renamed ")]);

        // Assert: the same artist row carries the new (trimmed) name, and keeps its album
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([artist.Id, other.Id], ignoreOrder: true);
        scenario.DbContext.Artists.Single(a => a.Id == artist.Id).Name.ShouldBe("Renamed");
        scenario.DbContext.Albums.Single(a => a.Id == album.Id).ArtistId.ShouldBe(artist.Id);

        // Assert: the song of its album is rewritten and moved to the folder of the new name
        var ownAfter = scenario.LoadSong(own.Id);
        ownAfter.AlbumId.ShouldBe(album.Id);
        ownAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([artist.Id]);
        ownAfter.Label.ShouldBe("Own - Renamed");
        ownAfter.Checksum.ShouldNotBe(own.Checksum);
        ownAfter.FileModifiedAt!.Value.ShouldBeGreaterThan(own.FileModifiedAt!.Value);
        ownAfter.RepositoryPath.ShouldBe("/data/admin/Renamed/Album/Own - Renamed.mp3");
        scenario.FileSystem.File.Exists(ownAfter.RepositoryPath).ShouldBeTrue();
        scenario.FileSystem.File.Exists(own.RepositoryPath).ShouldBeFalse();
        ownAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        // Assert: so is the song of another artist's album it is featured in
        var featuredAfter = scenario.LoadSong(featured.Id);
        featuredAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([other.Id, artist.Id]);
        featuredAfter.Label.ShouldBe("Featured - Other, Renamed");
        featuredAfter.Checksum.ShouldNotBe(featured.Checksum);
        featuredAfter.RepositoryPath.ShouldBe("/data/admin/Other/Their Album/Featured - Other, Renamed.mp3");
        scenario.FileSystem.File.Exists(featuredAfter.RepositoryPath).ShouldBeTrue();
        featuredAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        // Assert: a song the artist has nothing to do with is left alone
        var unrelatedAfter = scenario.LoadSong(unrelated.Id);
        unrelatedAfter.Checksum.ShouldBe(unrelated.Checksum);
        unrelatedAfter.RepositoryPath.ShouldBe(unrelated.RepositoryPath);
        unrelatedAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task EditAsync_NameUnchanged_TouchesNothing()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", scenario.CreateAlbum("Album", artist), device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, "Artist")]);

        // Assert
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Artists.Single().Name.ShouldBe("Artist");
    }

    [Fact]
    public async Task EditAsync_NameOfAnotherArtist_KeepsBothArtistsApart()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var namesake = scenario.CreateArtist("Namesake");
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", scenario.CreateAlbum("Album", artist), device);
        var namesakeSong = await scenario.CreateSyncedSongAsync("Their Song",
            scenario.CreateAlbum("Their Album", namesake), device);

        // Act: artist names are not unique, so this is not a merge
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, "Namesake")]);

        // Assert: two artists share the name, each still with its own song
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Where(a => a.Name == "Namesake").Select(a => a.Id).ToList()
            .ShouldBe([artist.Id, namesake.Id], ignoreOrder: true);
        var songAfter = scenario.LoadSong(song.Id);
        songAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([artist.Id]);
        songAfter.RepositoryPath.ShouldBe("/data/admin/Namesake/Album/Song - Namesake.mp3");

        var namesakeSongAfter = scenario.LoadSong(namesakeSong.Id);
        namesakeSongAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([namesake.Id]);
        namesakeSongAfter.Checksum.ShouldBe(namesakeSong.Checksum);
    }

    [Fact]
    public async Task EditAsync_SeveralArtists_RenamesThemAllAndRewritesASharedSongOnce()
    {
        // Arrange
        var scenario = new Scenario();
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var device = scenario.CreateDevice("Phone");
        var shared = await scenario.CreateSyncedSongAsync("Shared", scenario.CreateAlbum("Album", first), device,
            [first, second]);

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id,
            [Edit(first, "First (Renamed)"), Edit(second, "Second (Renamed)")]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        var sharedAfter = scenario.LoadSong(shared.Id);
        sharedAfter.Label.ShouldBe("Shared - First (Renamed), Second (Renamed)");
        sharedAfter.RepositoryPath.ShouldBe(
            "/data/admin/First (Renamed)/Album/Shared - First (Renamed), Second (Renamed).mp3");
        scenario.ReadMusicFiles().Keys.ShouldBe([sharedAfter.RepositoryPath]);
    }

    [Fact]
    public async Task EditAsync_ArtistWithoutSongs_RenamesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, "Renamed")]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Single().Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task EditAsync_RenamedPlaceholderArtist_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song",
            scenario.CreateAlbum(Album.PlaceholderName, placeholder), device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(placeholder, "Renamed")]));

        // Assert
        exception.Message.ShouldBe(
            "The artist '(No Artist)' cannot be renamed: it holds the songs that have no artist");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Artists.Single().Name.ShouldBe(Artist.PlaceholderName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EditAsync_EmptyName_ThrowsValidationException(string name)
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, name)]));

        // Assert
        exception.Message.ShouldBe("Artist name cannot be empty");
        scenario.DbContext.Artists.Single().Name.ShouldBe("Artist");
    }

    [Fact]
    public async Task EditAsync_NameTooLong_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, new string('a', 257))]));

        // Assert
        exception.Message.ShouldBe("Artist name cannot be longer than 256 characters");
    }

    [Fact]
    public async Task EditAsync_ArtistEditedTwice_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(artist, "One"), Edit(artist, "Two")]));

        // Assert
        exception.Message.ShouldBe("An artist cannot be edited more than once in the same operation");
        scenario.DbContext.Artists.Single().Name.ShouldBe("Artist");
    }

    [Fact]
    public async Task EditAsync_ArtistOfAnotherOwner_ThrowsArtistNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var mine = scenario.CreateArtist("Mine");
        var othersArtist = scenario.CreateArtist("Theirs", other.Id);

        // Act
        var exception = await Should.ThrowAsync<ArtistNotFoundException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id,
                [Edit(mine, "Mine (Renamed)"), Edit(othersArtist, "Renamed")]));

        // Assert: not even the owner's own artist in the list is renamed
        exception.Message.ShouldBe($"Artist not found with id {othersArtist.Id}");
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.Artists.Select(a => a.Name).ToList().ShouldBe(["Mine", "Theirs"], ignoreOrder: true);
    }

    [Fact]
    public async Task EditAsync_SecondSongFails_RestoresEveryArtistAndEverySongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", album, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), second.Id));

        // Act: the first song's file is rewritten and moved before the second song fails
        await Should.ThrowAsync<IOException>(() =>
            service.EditAsync(scenario.AdminUser.Id, [Edit(artist, "Renamed")]));

        // Assert
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        scenario.DbContext.Artists.Single().Name.ShouldBe("Artist");
    }
}
