using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Artists;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Artists;

public class ArtistRemoveServiceSpecs
{
    private static ArtistRemoveService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(songFileUpdate),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<ArtistRemoveService>>());

    [Fact]
    public async Task RemoveAsync_ArtistFeaturedInAnotherArtistsSong_OnlyTakesTheArtistOutOfTheSong()
    {
        // Arrange
        var scenario = new Scenario();
        var main = scenario.CreateArtist("Main");
        var featured = scenario.CreateArtist("Featured");
        var album = scenario.CreateAlbum("Album", main);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device, [main, featured]);
        var unrelated = await scenario.CreateSyncedSongAsync("Unrelated", album, device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [featured.Id]);

        // Assert: the song keeps its album, and its file, label and path no longer mention the artist
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([main.Id]);

        var updated = scenario.LoadSong(song.Id);
        updated.AlbumId.ShouldBe(album.Id);
        updated.Artists.Select(sa => sa.ArtistId).ShouldBe([main.Id]);
        updated.Label.ShouldBe("Song - Main");
        updated.Checksum.ShouldNotBe(song.Checksum);
        updated.RepositoryPath.ShouldBe("/data/admin/Main/Album/Song - Main.mp3");
        scenario.FileSystem.File.Exists(updated.RepositoryPath).ShouldBeTrue();
        scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeFalse();
        updated.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        // Assert: a song the artist had nothing to do with is left alone
        var unrelatedAfter = scenario.LoadSong(unrelated.Id);
        unrelatedAfter.Checksum.ShouldBe(unrelated.Checksum);
        unrelatedAfter.RepositoryPath.ShouldBe(unrelated.RepositoryPath);
        unrelatedAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task RemoveAsync_AlbumArtistOfASongWithOtherArtists_MovesItToThePlaceholderAlbumOfTheFirstRemainingArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var second = scenario.CreateArtist("Second");
        var third = scenario.CreateArtist("Third");
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device, [artist, second, third]);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id]);

        // Assert: the artist and its album are gone
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([second.Id, third.Id], ignoreOrder: true);
        scenario.DbContext.Albums.Any(a => a.Id == album.Id).ShouldBeFalse();

        // Assert: the song is in "(No Album)" of its first remaining artist
        var updated = scenario.LoadSong(song.Id);
        updated.Artists.Select(sa => sa.ArtistId).ShouldBe([second.Id, third.Id]);
        updated.Album.Name.ShouldBe(Album.PlaceholderName);
        updated.Album.ArtistId.ShouldBe(second.Id);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([updated.AlbumId]);
        updated.Checksum.ShouldNotBe(song.Checksum);
        updated.RepositoryPath.ShouldStartWith("/data/admin/Second/(No Album)/");
        scenario.FileSystem.File.Exists(updated.RepositoryPath).ShouldBeTrue();
        scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeFalse();
        updated.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
    }

    [Fact]
    public async Task RemoveAsync_SoleArtistOfItsSongs_MovesThemToThePlaceholderArtistAndAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", scenario.CreateAlbum("One", artist), device);
        var second = await scenario.CreateSyncedSongAsync("Second", scenario.CreateAlbum("Two", artist), device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id]);

        // Assert: one "(No Artist)" artist with one "(No Album)" album takes both songs in
        scenario.DbContext.ChangeTracker.Clear();
        var placeholderArtist = scenario.DbContext.Artists.Single();
        placeholderArtist.Name.ShouldBe(Artist.PlaceholderName);
        var placeholderAlbum = scenario.DbContext.Albums.Single();
        placeholderAlbum.Name.ShouldBe(Album.PlaceholderName);
        placeholderAlbum.ArtistId.ShouldBe(placeholderArtist.Id);

        foreach (var before in new[] { first, second })
        {
            var song = scenario.LoadSong(before.Id);
            song.Artists.Select(sa => sa.ArtistId).ShouldBe([placeholderArtist.Id]);
            song.AlbumId.ShouldBe(placeholderAlbum.Id);
            song.Label.ShouldBe($"{before.Title} - (No Artist)");
            song.RepositoryPath.ShouldBe($"/data/admin/(No Artist)/(No Album)/{before.Title} - (No Artist).mp3");
            scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeTrue();
            song.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
        }
    }

    [Fact]
    public async Task RemoveAsync_PlaceholderArtistAlreadyExists_ReusesItAndItsPlaceholderAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholderArtist = scenario.CreateArtist(Artist.PlaceholderName);
        var placeholderAlbum = scenario.CreateAlbum(Album.PlaceholderName, placeholderArtist);
        var artist = scenario.CreateArtist("Artist");
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", scenario.CreateAlbum("Album", artist), device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([placeholderArtist.Id]);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([placeholderAlbum.Id]);
        scenario.LoadSong(song.Id).AlbumId.ShouldBe(placeholderAlbum.Id);
    }

    [Fact]
    public async Task RemoveAsync_SongOfTheArtistsAlbumNotPerformedByTheArtist_KeepsItsArtistsAndMovesIt()
    {
        // Arrange: legacy data, since the album artist is normally one of the song's artists
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var performer = scenario.CreateArtist("Performer");
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", scenario.CreateAlbum("Temp", performer), device);
        var tracked = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        tracked.AlbumId = album.Id;
        scenario.DbContext.SaveChanges();
        scenario.DbContext.ChangeTracker.Clear();

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        var updated = scenario.LoadSong(song.Id);
        updated.Artists.Select(sa => sa.ArtistId).ShouldBe([performer.Id]);
        updated.Album.Name.ShouldBe(Album.PlaceholderName);
        updated.Album.ArtistId.ShouldBe(performer.Id);
        scenario.DbContext.Artists.Any(a => a.Id == artist.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsync_ArtistWithoutSongs_DeletesItWithItsEmptyAlbumsAndUnusedArtworks()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var photo = new Artwork { Data = [1], MimeType = "image/png", Width = 1, Height = 1 };
        var cover = new Artwork { Data = [2], MimeType = "image/png", Width = 1, Height = 1 };
        artist.Photo = photo;
        scenario.CreateAlbum("Album", artist).Cover = cover;
        scenario.CreateAlbum("Other Album", artist);
        scenario.DbContext.SaveChanges();
        var otherArtist = scenario.CreateArtist("Other Artist");

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([otherArtist.Id]);
        scenario.DbContext.Albums.ShouldBeEmpty();
        scenario.DbContext.Artworks.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_PlaceholderArtistWithSongs_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        var album = scenario.CreateAlbum(Album.PlaceholderName, placeholder);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [placeholder.Id]));

        // Assert
        exception.Message.ShouldContain("cannot be deleted while it still has songs");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Artists.Any(a => a.Id == placeholder.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_PlaceholderArtistWithoutSongs_DeletesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        scenario.CreateAlbum(Album.PlaceholderName, placeholder);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [placeholder.Id]);

        // Assert
        scenario.DbContext.Artists.ShouldBeEmpty();
        scenario.DbContext.Albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_ArtistOfAnotherOwner_ThrowsArtistNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var exception = await Should.ThrowAsync<ArtistNotFoundException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [othersArtist.Id]));

        // Assert
        exception.Message.ShouldBe($"Artist not found with id {othersArtist.Id}");
        scenario.DbContext.Artists.Any(a => a.Id == othersArtist.Id).ShouldBeTrue();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_SecondSongFails_RestoresTheArtistItsAlbumsAndEverySongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var other = scenario.CreateArtist("Other");
        var album = scenario.CreateAlbum("Album", artist);
        var otherAlbum = scenario.CreateAlbum("Other Album", other);
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", otherAlbum, device, [other, artist]);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), second.Id));

        // Act: the first song already moved to the placeholder artist and album when the second one fails
        await Should.ThrowAsync<IOException>(() => service.RemoveAsync(scenario.AdminUser.Id, [artist.Id]));

        // Assert: neither placeholder is left behind
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([artist.Id, other.Id], ignoreOrder: true);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([album.Id, otherAlbum.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task RemoveAsync_CommitFails_RestoresTheArtistItsAlbumsAndEverySongRowAndFile()
    {
        // Arrange
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);
        var filesBefore = scenario.ReadMusicFiles();
        interceptor.Armed = true;

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id]));

        // Assert
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([artist.Id]);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([album.Id]);
    }

    [Fact]
    public async Task RemoveAsync_TakesTheLocksOfEveryArtistAndAlbumInvolved_OnceUpFront()
    {
        // Arrange
        var scenario = new Scenario();
        var ownerId = scenario.AdminUser.Id;
        var artist = scenario.CreateArtist("Artist");
        var other = scenario.CreateArtist("Other");
        var device = scenario.CreateDevice("Phone");
        // Left without artists: goes to the placeholder album of the placeholder artist
        await scenario.CreateSyncedSongAsync("Solo", scenario.CreateAlbum("Album", artist), device);
        // Left with one artist: goes to that artist's placeholder album
        await scenario.CreateSyncedSongAsync("Duet", scenario.CreateAlbum("Duets", artist), device, [artist, other]);
        scenario.CreateAlbum("Empty", artist);
        scenario.AdvisoryLocks.Acquisitions.Clear();

        // Act
        await CreateService(scenario).RemoveAsync(ownerId, [artist.Id]);

        // Assert: one acquisition of artist and album keys, before any song (and its file locks) is touched
        var acquisitions = scenario.AdvisoryLocks.Acquisitions.ToList();
        acquisitions[0].ShouldBe(AdvisoryLockKey.Normalize(
        [
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Artist"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Other"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, Artist.PlaceholderName),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "Album"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "Duets"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "Empty"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Other", Album.PlaceholderName),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, Artist.PlaceholderName, Album.PlaceholderName),
        ]));
        var fileClassId = AdvisoryLockKey.Create(AdvisoryLockScope.File, ownerId, "").ClassId;
        acquisitions.Skip(1).SelectMany(keys => keys).ShouldAllBe(key => key.ClassId == fileClassId);
    }

    [Fact]
    public async Task RemoveAsync_SeveralArtists_TakesThemAllOutOfTheirSongsAndDeletesTheirAlbums()
    {
        // Arrange
        var scenario = new Scenario();
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var kept = scenario.CreateArtist("Kept");
        var firstAlbum = scenario.CreateAlbum("First Album", first);
        var secondAlbum = scenario.CreateAlbum("Second Album", second);
        var keptAlbum = scenario.CreateAlbum("Kept Album", kept);
        var device = scenario.CreateDevice("Phone");
        // Performed only by artists being deleted: left without any
        var duet = await scenario.CreateSyncedSongAsync("Duet", firstAlbum, device, [first, second]);
        // In an album being deleted, with an artist that stays
        var shared = await scenario.CreateSyncedSongAsync("Shared", secondAlbum, device, [second, kept]);
        // Only features artists being deleted
        var featuring = await scenario.CreateSyncedSongAsync("Featuring", keptAlbum, device, [kept, first, second]);
        var untouched = await scenario.CreateSyncedSongAsync("Untouched", keptAlbum, device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [first.Id, second.Id]);

        // Assert: both artists and their albums are gone
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        var placeholderArtist = scenario.DbContext.Artists.Single(a => a.Name == Artist.PlaceholderName);
        scenario.DbContext.Artists.Select(a => a.Id).ToList()
            .ShouldBe([kept.Id, placeholderArtist.Id], ignoreOrder: true);
        scenario.DbContext.Albums.Any(a => a.Id == firstAlbum.Id || a.Id == secondAlbum.Id).ShouldBeFalse();

        var duetAfter = scenario.LoadSong(duet.Id);
        duetAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([placeholderArtist.Id]);
        duetAfter.Album.Name.ShouldBe(Album.PlaceholderName);
        duetAfter.Album.ArtistId.ShouldBe(placeholderArtist.Id);
        duetAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        var sharedAfter = scenario.LoadSong(shared.Id);
        sharedAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([kept.Id]);
        sharedAfter.Album.Name.ShouldBe(Album.PlaceholderName);
        sharedAfter.Album.ArtistId.ShouldBe(kept.Id);

        var featuringAfter = scenario.LoadSong(featuring.Id);
        featuringAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([kept.Id]);
        featuringAfter.AlbumId.ShouldBe(keptAlbum.Id);
        featuringAfter.RepositoryPath.ShouldBe("/data/admin/Kept/Kept Album/Featuring - Kept.mp3");

        var untouchedAfter = scenario.LoadSong(untouched.Id);
        untouchedAfter.Checksum.ShouldBe(untouched.Checksum);
        untouchedAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task RemoveAsync_OneOfSeveralArtistsNotFound_ThrowsAndDeletesNone()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var artist = scenario.CreateArtist("Artist");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var exception = await Should.ThrowAsync<ArtistNotFoundException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id, othersArtist.Id]));

        // Assert
        exception.Message.ShouldBe($"Artist not found with id {othersArtist.Id}");
        scenario.DbContext.Artists.Count().ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAsync_SecondArtistsSongFails_RestoresEveryArtistAlbumSongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var firstAlbum = scenario.CreateAlbum("First Album", first);
        var secondAlbum = scenario.CreateAlbum("Second Album", second);
        var device = scenario.CreateDevice("Phone");
        var firstSong = await scenario.CreateSyncedSongAsync("First Song", firstAlbum, device);
        var secondSong = await scenario.CreateSyncedSongAsync("Second Song", secondAlbum, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), secondSong.Id));

        // Act: the first artist's song already moved when the second artist's song fails
        await Should.ThrowAsync<IOException>(() =>
            service.RemoveAsync(scenario.AdminUser.Id, [first.Id, second.Id]));

        // Assert: the first artist and its album are back too
        scenario.ShouldHaveUnchangedSongs([firstSong, secondSong], filesBefore);
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([first.Id, second.Id], ignoreOrder: true);
        scenario.DbContext.Albums.Select(a => a.Id).ToList()
            .ShouldBe([firstAlbum.Id, secondAlbum.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task RemoveAsync_EmptyPlaceholderArtistAlongWithASoleArtistOfSongs_ThrowsAndKeepsEverything()
    {
        // Arrange: the other artist's songs would move to the very placeholder artist being deleted
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        var artist = scenario.CreateArtist("Artist");
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", scenario.CreateAlbum("Album", artist), device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [artist.Id, placeholder.Id]));

        // Assert
        exception.Message.ShouldContain("their songs move to it");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Artists.Count().ShouldBe(2);
    }
}
