using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Common.Services.Artists;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Artists;

public class ArtistMergeServiceSpecs
{
    private static ArtistMergeService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null)
    {
        var songUpdate = scenario.CreateSongUpdateService(songFileUpdate);
        var albumDelete = new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>());
        var artistDelete = new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>());
        var artworkDelete = new ArtworkDeleteService(scenario.DbContext,
            Substitute.For<ILogger<ArtworkDeleteService>>());

        return new ArtistMergeService(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            songUpdate,
            new AlbumMergeService(scenario.DbContext, scenario.FileTransactions, scenario.AdvisoryLocks, songUpdate,
                albumDelete, artistDelete, artworkDelete, Substitute.For<ILogger<AlbumMergeService>>()),
            artistDelete,
            artworkDelete,
            Substitute.For<ILogger<ArtistMergeService>>());
    }

    private static Artwork CreateArtwork(Scenario scenario)
    {
        var artwork = new Artwork { Data = [1, 2, 3], MimeType = "image/png", Width = 1, Height = 1 };
        scenario.DbContext.Add(artwork);
        scenario.DbContext.SaveChanges();

        return artwork;
    }

    private static Source CreateSource(Scenario scenario, string name)
    {
        var source = new Source { Name = name, Icon = "icon", Address = "http://localhost", IsPaid = false };
        scenario.DbContext.Add(source);
        scenario.DbContext.SaveChanges();

        return source;
    }

    private static void AddLink(Scenario scenario, Artist artist, Source source, string externalId)
    {
        scenario.DbContext.Add(new ArtistSource { ArtistId = artist.Id, SourceId = source.Id, ExternalId = externalId });
        scenario.DbContext.SaveChanges();
    }

    [Fact]
    public async Task MergeAsync_ArtistWithItsOwnAlbum_GivesTheAlbumAndItsSongsToTheTarget()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");
        var targetAlbum = scenario.CreateAlbum("Target Album", target);
        var sourceAlbum = scenario.CreateAlbum("Source Album", source);
        var device = scenario.CreateDevice("Phone");
        var kept = await scenario.CreateSyncedSongAsync("Kept", targetAlbum, device);
        var first = await scenario.CreateSyncedSongAsync("First", sourceAlbum, device);
        var second = await scenario.CreateSyncedSongAsync("Second", sourceAlbum, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: the artist is gone, and its album, the same row, is now an album of the target
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([target.Id]);
        scenario.DbContext.Albums.Select(a => a.Id).ToList()
            .ShouldBe([targetAlbum.Id, sourceAlbum.Id], ignoreOrder: true);
        scenario.DbContext.Albums.ShouldAllBe(a => a.ArtistId == target.Id);

        foreach (var before in new[] { first, second })
        {
            // Each song's file is rewritten and moved, and its device has to download it again
            var song = scenario.LoadSong(before.Id);
            song.AlbumId.ShouldBe(sourceAlbum.Id);
            song.Artists.Select(sa => sa.ArtistId).ShouldBe([target.Id]);
            song.Label.ShouldBe($"{before.Title} - Target");
            song.Checksum.ShouldNotBe(before.Checksum);
            song.FileModifiedAt!.Value.ShouldBeGreaterThan(before.FileModifiedAt!.Value);
            song.RepositoryPath.ShouldBe($"/data/admin/Target/Source Album/{before.Title} - Target.mp3");
            scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeTrue();
            scenario.FileSystem.File.Exists(before.RepositoryPath).ShouldBeFalse();
            song.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
        }

        // Assert: a song the merged artist had nothing to do with is left alone
        var keptAfter = scenario.LoadSong(kept.Id);
        keptAfter.Checksum.ShouldBe(kept.Checksum);
        keptAfter.RepositoryPath.ShouldBe(kept.RepositoryPath);
        keptAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task MergeAsync_ArtistFeaturedInOtherArtistsSongs_PutsTheTargetInItsPlaceOnce()
    {
        // Arrange
        var scenario = new Scenario();
        var main = scenario.CreateArtist("Main");
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");
        var album = scenario.CreateAlbum("Album", main);
        var device = scenario.CreateDevice("Phone");
        var featuring = await scenario.CreateSyncedSongAsync("Featuring", album, device, [main, source]);
        var both = await scenario.CreateSyncedSongAsync("Both", album, device, [source, main, target]);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: the songs keep their album, and the target takes the artist's place in their artists
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([main.Id, target.Id], ignoreOrder: true);

        var featuringAfter = scenario.LoadSong(featuring.Id);
        featuringAfter.AlbumId.ShouldBe(album.Id);
        featuringAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([main.Id, target.Id]);
        featuringAfter.Checksum.ShouldNotBe(featuring.Checksum);
        featuringAfter.RepositoryPath.ShouldStartWith("/data/admin/Main/Album/Featuring - ");
        featuringAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        // Assert: a song that already had the target has it only once
        scenario.LoadSong(both.Id).Artists.Select(sa => sa.ArtistId).ShouldBe([target.Id, main.Id]);
    }

    [Fact]
    public async Task MergeAsync_AlbumNameTheTargetAlreadyHas_MergesTheAlbumIntoTheTargets()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");
        var targetAlbum = scenario.CreateAlbum("Album", target);
        var sourceAlbum = scenario.CreateAlbum("Album", source);
        var cover = CreateArtwork(scenario);
        sourceAlbum.CoverId = cover.Id;
        sourceAlbum.Year = 2002;
        scenario.DbContext.SaveChanges();
        var device = scenario.CreateDevice("Phone");
        var kept = await scenario.CreateSyncedSongAsync("Kept", targetAlbum, device);
        var moved = await scenario.CreateSyncedSongAsync("Moved", sourceAlbum, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: a single album remains, the target's, with the cover and year it was missing
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([target.Id]);
        var album = scenario.DbContext.Albums.Single();
        album.Id.ShouldBe(targetAlbum.Id);
        album.CoverId.ShouldBe(cover.Id);
        album.Year.ShouldBe(2002);

        var movedAfter = scenario.LoadSong(moved.Id);
        movedAfter.AlbumId.ShouldBe(targetAlbum.Id);
        movedAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([target.Id]);
        movedAfter.RepositoryPath.ShouldBe("/data/admin/Target/Album/Moved - Target.mp3");
        scenario.FileSystem.File.Exists(movedAfter.RepositoryPath).ShouldBeTrue();
        movedAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        scenario.LoadSong(kept.Id).Checksum.ShouldBe(kept.Checksum);
    }

    [Fact]
    public async Task MergeAsync_ArtistsSharingAnAlbumNameTheTargetLacks_KeepsTheAlbumOfTheFirstOne()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var secondAlbum = scenario.CreateAlbum("Album", second);
        var firstAlbum = scenario.CreateAlbum("Album", first);
        var device = scenario.CreateDevice("Phone");
        var firstSong = await scenario.CreateSyncedSongAsync("First Song", firstAlbum, device);
        var secondSong = await scenario.CreateSyncedSongAsync("Second Song", secondAlbum, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [first.Id, second.Id]);

        // Assert: the order the artists were given in decides which album stays
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([target.Id]);
        var album = scenario.DbContext.Albums.Single();
        album.Id.ShouldBe(firstAlbum.Id);
        album.ArtistId.ShouldBe(target.Id);

        foreach (var before in new[] { firstSong, secondSong })
        {
            var song = scenario.LoadSong(before.Id);
            song.AlbumId.ShouldBe(firstAlbum.Id);
            song.Artists.Select(sa => sa.ArtistId).ShouldBe([target.Id]);
            song.RepositoryPath.ShouldBe($"/data/admin/Target/Album/{before.Title} - Target.mp3");
        }
    }

    [Fact]
    public async Task MergeAsync_SongOfTheArtistsAlbumNotPerformedByIt_GainsTheTarget()
    {
        // Arrange: the album artist does not perform the song of its album
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");
        var performer = scenario.CreateArtist("Performer");
        var album = scenario.CreateAlbum("Album", source);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device, [performer]);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: the album artist is always one of the song's artists
        scenario.DbContext.ChangeTracker.Clear();
        var updated = scenario.LoadSong(song.Id);
        updated.Album.ArtistId.ShouldBe(target.Id);
        updated.Artists.Select(sa => sa.ArtistId).ShouldBe([performer.Id, target.Id]);
        updated.RepositoryPath.ShouldStartWith("/data/admin/Target/Album/Song - ");
    }

    [Fact]
    public async Task MergeAsync_ArtistWithTheSameNameAsTheTarget_LeavesTheSongFilesAsTheyAre()
    {
        // Arrange: artist names are not unique, and two artists with the same name are the usual ones to merge
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Artist");
        var duplicate = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", duplicate);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [duplicate.Id]);

        // Assert: the song says the same about itself, so its file and its devices are left alone
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([target.Id]);
        var updated = scenario.LoadSong(song.Id);
        updated.Artists.Select(sa => sa.ArtistId).ShouldBe([target.Id]);
        updated.Album.ArtistId.ShouldBe(target.Id);
        updated.Checksum.ShouldBe(song.Checksum);
        updated.RepositoryPath.ShouldBe(song.RepositoryPath);
        updated.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task MergeAsync_TargetWithoutPhotoAndBackground_TakesThemFromTheFirstArtistThatHasThem()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var targetPhoto = CreateArtwork(scenario);
        var firstPhoto = CreateArtwork(scenario);
        var secondBackground = CreateArtwork(scenario);
        target.PhotoId = targetPhoto.Id;
        first.PhotoId = firstPhoto.Id;
        second.BackgroundId = secondBackground.Id;
        scenario.DbContext.SaveChanges();

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [first.Id, second.Id]);

        // Assert: the target keeps its own photo, and the one that was not taken is deleted
        scenario.DbContext.ChangeTracker.Clear();
        var merged = scenario.DbContext.Artists.Single();
        merged.Id.ShouldBe(target.Id);
        merged.PhotoId.ShouldBe(targetPhoto.Id);
        merged.BackgroundId.ShouldBe(secondBackground.Id);
        scenario.DbContext.Artworks.Select(a => a.Id).ToList()
            .ShouldBe([targetPhoto.Id, secondBackground.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task MergeAsync_ArtistsLinkedToExternalSources_MovesTheLinksTheTargetDoesNotHave()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var store = CreateSource(scenario, "Store");
        var otherStore = CreateSource(scenario, "Other Store");
        AddLink(scenario, target, store, "shared");
        AddLink(scenario, first, store, "shared");
        AddLink(scenario, first, store, "first");
        AddLink(scenario, first, otherStore, "shared");
        AddLink(scenario, second, store, "first");
        AddLink(scenario, second, store, "second");

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [first.Id, second.Id]);

        // Assert: one link per source and external id, all of the target
        scenario.DbContext.ChangeTracker.Clear();
        var links = scenario.DbContext.ArtistSources.ToList();
        links.ShouldAllBe(link => link.ArtistId == target.Id);
        links.Select(link => (link.SourceId, link.ExternalId)).ShouldBe(
            [(store.Id, "shared"), (store.Id, "first"), (otherStore.Id, "shared"), (store.Id, "second")],
            ignoreOrder: true);
    }

    [Fact]
    public async Task MergeAsync_IntoThePlaceholderArtist_MovesTheSongsToIt()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, placeholder.Id, [artist.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([placeholder.Id]);
        scenario.LoadSong(song.Id).Artists.Select(sa => sa.ArtistId).ShouldBe([placeholder.Id]);
    }

    [Fact]
    public async Task MergeAsync_PlaceholderArtistIntoAnother_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        var target = scenario.CreateArtist("Artist");
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song",
            scenario.CreateAlbum(Album.PlaceholderName, placeholder), device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [placeholder.Id]));

        // Assert
        exception.Message.ShouldBe(
            "The artist '(No Artist)' cannot be merged into another artist: it holds the songs that have no artist");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Artists.Count().ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_NoArtistsToMerge_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Artist");

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, []));

        // Assert
        exception.Message.ShouldBe("There are no artists to merge");
    }

    [Fact]
    public async Task MergeAsync_TargetAmongTheArtistsToMerge_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id, target.Id]));

        // Assert
        exception.Message.ShouldBe("An artist cannot be merged into itself");
        scenario.DbContext.Artists.Count().ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_ArtistOfAnotherOwner_ThrowsArtistNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateArtist("Target");
        var mine = scenario.CreateArtist("Mine");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var exception = await Should.ThrowAsync<ArtistNotFoundException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [mine.Id, othersArtist.Id]));

        // Assert: not even the owner's own artist in the list is merged
        exception.Message.ShouldBe($"Artist not found with id {othersArtist.Id}");
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.Artists.Count().ShouldBe(3);
    }

    [Fact]
    public async Task MergeAsync_SecondSongFails_RestoresEveryArtistAlbumAndSongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");
        var targetAlbum = scenario.CreateAlbum("Shared", target);
        var ownAlbum = scenario.CreateAlbum("Own", source);
        var sharedAlbum = scenario.CreateAlbum("Shared", source);
        var photo = CreateArtwork(scenario);
        source.PhotoId = photo.Id;
        scenario.DbContext.SaveChanges();
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", ownAlbum, device);
        var second = await scenario.CreateSyncedSongAsync("Second", sharedAlbum, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), second.Id));

        // Act: the first song's album is given to the target, and the song rewritten, before the second one fails
        await Should.ThrowAsync<IOException>(() =>
            service.MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]));

        // Assert
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([target.Id, source.Id], ignoreOrder: true);
        scenario.DbContext.Artists.Single(a => a.Id == target.Id).PhotoId.ShouldBeNull();
        scenario.DbContext.Albums.Single(a => a.Id == targetAlbum.Id).ArtistId.ShouldBe(target.Id);
        scenario.DbContext.Albums.Single(a => a.Id == ownAlbum.Id).ArtistId.ShouldBe(source.Id);
        scenario.DbContext.Albums.Single(a => a.Id == sharedAlbum.Id).ArtistId.ShouldBe(source.Id);
    }

    [Fact]
    public async Task PreviewAsync_CountsTheSongsThatChangeAndTheAlbumsThatAreMerged()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var other = scenario.CreateArtist("Other");
        var targetAlbum = scenario.CreateAlbum("Shared", target);
        var firstShared = scenario.CreateAlbum("Shared", first);
        var firstOwn = scenario.CreateAlbum("Own", first);
        scenario.CreateAlbum("Own", second);
        var otherAlbum = scenario.CreateAlbum("Other Album", other);
        scenario.CreateSong("Targets", album: targetAlbum);
        scenario.CreateSong("In Shared", album: firstShared);
        scenario.CreateSong("In Own", album: firstOwn, artists: [first, second]);
        scenario.CreateSong("Featuring", album: otherAlbum, artists: [other, second]);
        scenario.CreateSong("Unrelated", album: otherAlbum);

        // Act
        var preview = await CreateService(scenario)
            .PreviewAsync(scenario.AdminUser.Id, target.Id, [first.Id, second.Id]);

        // Assert: "Shared" joins the target's, and the second "Own" joins the first one; nothing is changed
        preview.ShouldBe(new ArtistMergePreview(3, 2));
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Count().ShouldBe(4);
        scenario.DbContext.Albums.Count().ShouldBe(5);
    }

    [Fact]
    public async Task PreviewAsync_ArtistOfAnotherOwner_ThrowsArtistNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateArtist("Target");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act & Assert
        await Should.ThrowAsync<ArtistNotFoundException>(() =>
            CreateService(scenario).PreviewAsync(scenario.AdminUser.Id, target.Id, [othersArtist.Id]));
    }
}
