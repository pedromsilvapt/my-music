using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Albums;

public class AlbumRemoveServiceSpecs
{
    private static AlbumRemoveService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(songFileUpdate),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<AlbumRemoveService>>());

    [Fact]
    public async Task RemoveAsync_AlbumWithSongs_MovesThemToThePlaceholderAlbumOfTheSameArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", album, device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id]);

        // Assert: the album is gone, and both songs share one placeholder album of the same album artist
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Any(a => a.Id == album.Id).ShouldBeFalse();
        var placeholder = scenario.DbContext.Albums.Single();
        placeholder.Name.ShouldBe(Album.PlaceholderName);
        placeholder.ArtistId.ShouldBe(artist.Id);

        foreach (var before in new[] { first, second })
        {
            // Each song's file is rewritten and moved, and its device has to download it again
            var song = scenario.LoadSong(before.Id);
            song.AlbumId.ShouldBe(placeholder.Id);
            song.Artists.Select(sa => sa.ArtistId).ShouldBe([artist.Id]);
            song.Checksum.ShouldNotBe(before.Checksum);
            song.FileModifiedAt!.Value.ShouldBeGreaterThan(before.FileModifiedAt!.Value);
            song.RepositoryPath.ShouldBe($"/data/admin/Artist/(No Album)/{before.Title} - Artist.mp3");
            scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeTrue();
            scenario.FileSystem.File.Exists(before.RepositoryPath).ShouldBeFalse();
            song.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
        }
    }

    [Fact]
    public async Task RemoveAsync_ArtistAlreadyHasPlaceholderAlbum_MovesTheSongsIntoIt()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);
        var untouched = await scenario.CreateSyncedSongAsync("Untouched", placeholder, device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id]);

        // Assert: no second placeholder album, and the song already in it is left alone
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([placeholder.Id]);
        scenario.LoadSong(song.Id).AlbumId.ShouldBe(placeholder.Id);

        var untouchedAfter = scenario.LoadSong(untouched.Id);
        untouchedAfter.Checksum.ShouldBe(untouched.Checksum);
        untouchedAfter.RepositoryPath.ShouldBe(untouched.RepositoryPath);
        untouchedAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task RemoveAsync_EmptyAlbum_DeletesItWithItsSourcesAndUnusedCover()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var cover = new Artwork { Data = [1, 2, 3], MimeType = "image/png", Width = 1, Height = 1 };
        album.Cover = cover;
        scenario.DbContext.SaveChanges();

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id]);

        // Assert: the artist stays, even with no albums or songs left
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.ShouldBeEmpty();
        scenario.DbContext.Artworks.Any(a => a.Id == cover.Id).ShouldBeFalse();
        scenario.DbContext.Artists.Any(a => a.Id == artist.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_CoverUsedByASong_KeepsTheCover()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var cover = new Artwork { Data = [1, 2, 3], MimeType = "image/png", Width = 1, Height = 1 };
        album.Cover = cover;
        scenario.DbContext.SaveChanges();
        var otherAlbum = scenario.CreateAlbum("Other Album", scenario.CreateArtist("Other Artist"));
        scenario.CreateSong("Other Song", album: otherAlbum, coverId: cover.Id);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artworks.Any(a => a.Id == cover.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_PlaceholderAlbumWithSongs_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", placeholder, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [placeholder.Id]));

        // Assert
        exception.Message.ShouldContain("cannot be deleted while it still has songs");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Albums.Any(a => a.Id == placeholder.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_EmptyPlaceholderAlbum_DeletesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [placeholder.Id]);

        // Assert
        scenario.DbContext.Albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_AlbumOfAnotherOwner_ThrowsAlbumNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var exception = await Should.ThrowAsync<AlbumNotFoundException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [othersAlbum.Id]));

        // Assert
        exception.Message.ShouldBe($"Album not found with id {othersAlbum.Id}");
        scenario.DbContext.Albums.Any(a => a.Id == othersAlbum.Id).ShouldBeTrue();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_SecondSongFails_RestoresTheAlbumAndEverySongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", album, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), second.Id));

        // Act: the first song's file is rewritten and moved before the second song fails
        await Should.ThrowAsync<IOException>(() => service.RemoveAsync(scenario.AdminUser.Id, [album.Id]));

        // Assert: not even the placeholder album the first song moved to is left behind
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([album.Id]);
    }

    [Fact]
    public async Task RemoveAsync_CommitFails_RestoresTheAlbumAndEverySongRowAndFile()
    {
        // Arrange
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", album, device);
        var second = await scenario.CreateSyncedSongAsync("Second", album, device);
        var filesBefore = scenario.ReadMusicFiles();
        interceptor.Armed = true;

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id]));

        // Assert
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([album.Id]);
    }

    [Fact]
    public async Task RemoveAsync_TakesTheLocksOfTheAlbumAndOfThePlaceholderAlbum_OnceUpFront()
    {
        // Arrange
        var scenario = new Scenario();
        var ownerId = scenario.AdminUser.Id;
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var device = scenario.CreateDevice("Phone");
        await scenario.CreateSyncedSongAsync("First", album, device);
        await scenario.CreateSyncedSongAsync("Second", album, device);
        scenario.AdvisoryLocks.Acquisitions.Clear();

        // Act
        await CreateService(scenario).RemoveAsync(ownerId, [album.Id]);

        // Assert: one acquisition of artist and album keys, before any song (and its file locks) is touched
        var acquisitions = scenario.AdvisoryLocks.Acquisitions.ToList();
        acquisitions[0].ShouldBe(AdvisoryLockKey.Normalize(
        [
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Artist"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "Album"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", Album.PlaceholderName),
        ]));
        var fileClassId = AdvisoryLockKey.Create(AdvisoryLockScope.File, ownerId, "").ClassId;
        acquisitions.Skip(1).SelectMany(keys => keys).ShouldAllBe(key => key.ClassId == fileClassId);
    }

    [Fact]
    public async Task RemoveAsync_ReleasesItsLocks_SoTheSameAlbumNameCanBeLockedAgain()
    {
        // Arrange
        var scenario = new Scenario();
        var ownerId = scenario.AdminUser.Id;
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        await CreateService(scenario).RemoveAsync(ownerId, [album.Id]);

        // Act: would hang if the delete still held the key
        var hold = scenario.AdvisoryLocks.HoldAsync(
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "Album"));

        // Assert
        await (await hold.WaitAsync(TimeSpan.FromSeconds(5))).DisposeAsync();
    }

    [Fact]
    public async Task RemoveAsync_SeveralAlbums_DeletesThemAllMovingTheirSongsToEachArtistsPlaceholderAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var other = scenario.CreateArtist("Other");
        var first = scenario.CreateAlbum("First Album", artist);
        var second = scenario.CreateAlbum("Second Album", artist);
        var others = scenario.CreateAlbum("Others Album", other);
        var empty = scenario.CreateAlbum("Empty Album", other);
        var kept = scenario.CreateAlbum("Kept Album", artist);
        var device = scenario.CreateDevice("Phone");
        var firstSong = await scenario.CreateSyncedSongAsync("First", first, device);
        var secondSong = await scenario.CreateSyncedSongAsync("Second", second, device);
        var othersSong = await scenario.CreateSyncedSongAsync("Others", others, device);
        var keptSong = await scenario.CreateSyncedSongAsync("Kept", kept, device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [first.Id, second.Id, others.Id, empty.Id]);

        // Assert: only the album left out survives, next to one placeholder album per album artist
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Where(a => a.Name != Album.PlaceholderName).Select(a => a.Id).ToList()
            .ShouldBe([kept.Id]);
        var placeholder = scenario.DbContext.Albums.Single(a => a.Name == Album.PlaceholderName && a.ArtistId == artist.Id);
        var othersPlaceholder = scenario.DbContext.Albums.Single(a => a.Name == Album.PlaceholderName && a.ArtistId == other.Id);

        scenario.LoadSong(firstSong.Id).AlbumId.ShouldBe(placeholder.Id);
        scenario.LoadSong(secondSong.Id).AlbumId.ShouldBe(placeholder.Id);
        var othersAfter = scenario.LoadSong(othersSong.Id);
        othersAfter.AlbumId.ShouldBe(othersPlaceholder.Id);
        othersAfter.RepositoryPath.ShouldBe("/data/admin/Other/(No Album)/Others - Other.mp3");
        othersAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        var keptAfter = scenario.LoadSong(keptSong.Id);
        keptAfter.AlbumId.ShouldBe(kept.Id);
        keptAfter.Checksum.ShouldBe(keptSong.Checksum);
        keptAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task RemoveAsync_SeveralAlbums_TakesAllTheirLocksInOneAcquisition()
    {
        // Arrange
        var scenario = new Scenario();
        var ownerId = scenario.AdminUser.Id;
        var first = scenario.CreateAlbum("First Album", scenario.CreateArtist("Artist"));
        var second = scenario.CreateAlbum("Second Album", scenario.CreateArtist("Other"));

        // Act
        await CreateService(scenario).RemoveAsync(ownerId, [first.Id, second.Id]);

        // Assert
        scenario.AdvisoryLocks.Acquisitions.ShouldHaveSingleItem().ShouldBe(AdvisoryLockKey.Normalize(
        [
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Artist"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Other"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "First Album"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", Album.PlaceholderName),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Other", "Second Album"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Other", Album.PlaceholderName),
        ]));
    }

    [Fact]
    public async Task RemoveAsync_OneOfSeveralAlbumsNotFound_ThrowsAndDeletesNone()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var exception = await Should.ThrowAsync<AlbumNotFoundException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id, othersAlbum.Id]));

        // Assert
        exception.Message.ShouldBe($"Album not found with id {othersAlbum.Id}");
        scenario.DbContext.Albums.Count().ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAsync_SecondAlbumsSongFails_RestoresEveryAlbumSongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var first = scenario.CreateAlbum("First Album", artist);
        var second = scenario.CreateAlbum("Second Album", artist);
        var device = scenario.CreateDevice("Phone");
        var firstSong = await scenario.CreateSyncedSongAsync("First", first, device);
        var secondSong = await scenario.CreateSyncedSongAsync("Second", second, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), secondSong.Id));

        // Act: the first album's song already moved when the second album's song fails
        await Should.ThrowAsync<IOException>(() =>
            service.RemoveAsync(scenario.AdminUser.Id, [first.Id, second.Id]));

        // Assert: the first album is back too
        scenario.ShouldHaveUnchangedSongs([firstSong, secondSong], filesBefore);
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([first.Id, second.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task RemoveAsync_EmptyPlaceholderAlbumAlongWithASiblingAlbumWithSongs_ThrowsAndKeepsEverything()
    {
        // Arrange: the sibling's songs would move to the very placeholder album being deleted
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, artist);
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id, placeholder.Id]));

        // Assert
        exception.Message.ShouldContain("their songs move to it");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Albums.Count().ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAsync_EmptyPlaceholderAlbumAlongWithAnotherArtistsAlbumWithSongs_DeletesBoth()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));
        var other = scenario.CreateArtist("Other");
        var album = scenario.CreateAlbum("Album", other);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);

        // Act
        await CreateService(scenario).RemoveAsync(scenario.AdminUser.Id, [album.Id, placeholder.Id]);

        // Assert: the song went to the placeholder album of its own album artist
        scenario.DbContext.ChangeTracker.Clear();
        var remaining = scenario.DbContext.Albums.Single();
        remaining.ArtistId.ShouldBe(other.Id);
        remaining.Name.ShouldBe(Album.PlaceholderName);
        scenario.LoadSong(song.Id).AlbumId.ShouldBe(remaining.Id);
    }
}
