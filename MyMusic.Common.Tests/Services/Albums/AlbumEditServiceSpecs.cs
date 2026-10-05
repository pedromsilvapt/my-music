using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Albums;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Albums;

public class AlbumEditServiceSpecs
{
    private static AlbumEditService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(songFileUpdate),
            Substitute.For<ILogger<AlbumEditService>>());

    private static AlbumEditInput Edit(Album album, string name, int? year = null) =>
        new() { AlbumId = album.Id, Name = name, Year = year };

    [Fact]
    public async Task EditAsync_RenamedAlbum_KeepsItsRowAndRewritesItsSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var otherAlbum = scenario.CreateAlbum("Other Album", artist);
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", album, device);
        var untouched = await scenario.CreateSyncedSongAsync("Untouched", otherAlbum, device);

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, "Renamed", 1999)]);

        // Assert: the same album row carries the new name and year
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([album.Id, otherAlbum.Id], ignoreOrder: true);
        var renamed = scenario.DbContext.Albums.Single(a => a.Id == album.Id);
        renamed.Name.ShouldBe("Renamed");
        renamed.Year.ShouldBe(1999);

        foreach (var before in new[] { first, second })
        {
            // Each song's file is rewritten and moved, and its device has to download it again
            var song = scenario.LoadSong(before.Id);
            song.AlbumId.ShouldBe(album.Id);
            song.Checksum.ShouldNotBe(before.Checksum);
            song.FileModifiedAt!.Value.ShouldBeGreaterThan(before.FileModifiedAt!.Value);
            song.RepositoryPath.ShouldBe($"/data/admin/Artist/Renamed/{before.Title} - Artist.mp3");
            scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeTrue();
            scenario.FileSystem.File.Exists(before.RepositoryPath).ShouldBeFalse();
            song.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
        }

        // Assert: a song of another album is left alone
        var untouchedAfter = scenario.LoadSong(untouched.Id);
        untouchedAfter.Checksum.ShouldBe(untouched.Checksum);
        untouchedAfter.RepositoryPath.ShouldBe(untouched.RepositoryPath);
        untouchedAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task EditAsync_OnlyTheYearChanges_SavesTheAlbumWithoutTouchingItsSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act: the name only differs by the whitespace around it, which is trimmed
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, " Album ", 2001)]);

        // Assert
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        var edited = scenario.DbContext.Albums.Single();
        edited.Name.ShouldBe("Album");
        edited.Year.ShouldBe(2001);
    }

    [Fact]
    public async Task EditAsync_YearLeftOut_ClearsTheYear()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        album.Year = 1999;
        scenario.DbContext.SaveChanges();

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, "Album")]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Single().Year.ShouldBeNull();
    }

    [Fact]
    public async Task EditAsync_EmptyAlbum_RenamesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, "  Renamed  ")]);

        // Assert: the name is trimmed
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Single().Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task EditAsync_NameOfAnotherAlbumOfTheSameArtist_ThrowsPointingToMergeAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        scenario.CreateAlbum("Taken", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<AlbumAlreadyExistsException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, "Taken", 1999)]));

        // Assert
        exception.Message.ShouldBe(
            "Artist 'Artist' already has an album named 'Taken'. To join the two albums, merge them instead.");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        var unchanged = scenario.DbContext.Albums.Single(a => a.Id == album.Id);
        unchanged.Name.ShouldBe("Album");
        unchanged.Year.ShouldBeNull();
    }

    [Fact]
    public async Task EditAsync_NameOfAnAlbumOfAnotherArtist_RenamesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var othersAlbum = scenario.CreateAlbum("Greatest Hits", scenario.CreateArtist("Other Artist"));

        // Act: album names are only unique per artist
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, "Greatest Hits")]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Where(a => a.Name == "Greatest Hits").Select(a => a.Id).ToList()
            .ShouldBe([album.Id, othersAlbum.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task EditAsync_SeveralAlbums_EditsThemAllTogether()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var renamed = scenario.CreateAlbum("First Album", artist);
        var redated = scenario.CreateAlbum("Second Album", artist);
        var device = scenario.CreateDevice("Phone");
        var renamedSong = await scenario.CreateSyncedSongAsync("First", renamed, device);
        var redatedSong = await scenario.CreateSyncedSongAsync("Second", redated, device);

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id,
            [Edit(renamed, "First Album (Deluxe)", 1998), Edit(redated, "Second Album", 2002)]);

        // Assert: only the songs of the album that changed its name are rewritten
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Single(a => a.Id == renamed.Id).Year.ShouldBe(1998);
        scenario.DbContext.Albums.Single(a => a.Id == redated.Id).Year.ShouldBe(2002);
        scenario.LoadSong(renamedSong.Id).RepositoryPath
            .ShouldBe("/data/admin/Artist/First Album (Deluxe)/First - Artist.mp3");

        var redatedSongAfter = scenario.LoadSong(redatedSong.Id);
        redatedSongAfter.Checksum.ShouldBe(redatedSong.Checksum);
        redatedSongAfter.RepositoryPath.ShouldBe(redatedSong.RepositoryPath);
        redatedSongAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task EditAsync_TwoAlbumsOfAnArtistRenamedToTheSameName_ThrowsAndKeepsBoth()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var first = scenario.CreateAlbum("First", artist);
        var second = scenario.CreateAlbum("Second", artist);

        // Act
        await Should.ThrowAsync<AlbumAlreadyExistsException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(first, "Same"), Edit(second, "Same")]));

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Name).ToList().ShouldBe(["First", "Second"], ignoreOrder: true);
    }

    [Fact]
    public async Task EditAsync_AlbumEditedTwice_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, "One"), Edit(album, "Two")]));

        // Assert
        exception.Message.ShouldBe("An album cannot be edited more than once in the same operation");
        scenario.DbContext.Albums.Single().Name.ShouldBe("Album");
    }

    [Fact]
    public async Task EditAsync_RenamedPlaceholderAlbum_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", placeholder, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(placeholder, "Renamed")]));

        // Assert
        exception.Message.ShouldBe(
            "The album '(No Album)' of 'Artist' cannot be renamed: it holds the songs that have no album");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Albums.Single().Name.ShouldBe(Album.PlaceholderName);
    }

    [Fact]
    public async Task EditAsync_YearOfAPlaceholderAlbum_SavesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));

        // Act
        await CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(placeholder, Album.PlaceholderName, 2010)]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Single().Year.ShouldBe(2010);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EditAsync_EmptyName_ThrowsValidationException(string name)
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, name)]));

        // Assert
        exception.Message.ShouldBe("Album name cannot be empty");
        scenario.DbContext.Albums.Single().Name.ShouldBe("Album");
    }

    [Fact]
    public async Task EditAsync_NameTooLong_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id, [Edit(album, new string('a', 257))]));

        // Assert
        exception.Message.ShouldBe("Album name cannot be longer than 256 characters");
    }

    [Fact]
    public async Task EditAsync_AlbumOfAnotherOwner_ThrowsAlbumNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var mine = scenario.CreateAlbum("Mine", scenario.CreateArtist("Artist"));
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var exception = await Should.ThrowAsync<AlbumNotFoundException>(() =>
            CreateService(scenario).EditAsync(scenario.AdminUser.Id,
                [Edit(mine, "Mine (Renamed)"), Edit(othersAlbum, "Renamed")]));

        // Assert: not even the owner's own album in the list is edited
        exception.Message.ShouldBe($"Album not found with id {othersAlbum.Id}");
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.Albums.Select(a => a.Name).ToList().ShouldBe(["Mine", "Album"], ignoreOrder: true);
    }

    [Fact]
    public async Task EditAsync_SecondSongFails_RestoresEveryAlbumAndEverySongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var otherAlbum = scenario.CreateAlbum("Other Album", artist);
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", otherAlbum, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), second.Id));

        // Act: the first album's song is rewritten and moved before the second album's song fails
        await Should.ThrowAsync<IOException>(() => service.EditAsync(scenario.AdminUser.Id,
            [Edit(album, "Renamed", 1999), Edit(otherAlbum, "Other Renamed", 2000)]));

        // Assert
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        var albums = scenario.DbContext.Albums.OrderBy(a => a.Id).ToList();
        albums.Select(a => a.Name).ShouldBe(["Album", "Other Album"]);
        albums.ShouldAllBe(a => a.Year == null);
    }
}
