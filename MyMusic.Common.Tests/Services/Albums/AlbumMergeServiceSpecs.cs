using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Albums;

public class AlbumMergeServiceSpecs
{
    private static AlbumMergeService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(songFileUpdate),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<AlbumMergeService>>());

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

    private static void AddLink(Scenario scenario, Album album, Source source, string externalId)
    {
        scenario.DbContext.Add(new AlbumSource
        {
            AlbumId = album.Id,
            SourceId = source.Id,
            ExternalId = externalId,
            SongsCount = 1,
        });
        scenario.DbContext.SaveChanges();
    }

    [Fact]
    public async Task MergeAsync_AlbumsOfTheSameArtist_MovesTheirSongsToTheTargetAndDeletesThem()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Album", artist);
        var deluxe = scenario.CreateAlbum("Album (Deluxe)", artist);
        var remaster = scenario.CreateAlbum("Album (Remaster)", artist);
        var device = scenario.CreateDevice("Phone");
        var kept = await scenario.CreateSyncedSongAsync("Kept", target, device);
        var first = await scenario.CreateSyncedSongAsync("First", deluxe, device);
        var second = await scenario.CreateSyncedSongAsync("Second", remaster, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [deluxe.Id, remaster.Id]);

        // Assert: only the target album remains, along with its artist
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([target.Id]);
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([artist.Id]);

        foreach (var before in new[] { first, second })
        {
            // Each merged song's file is rewritten and moved, and its device has to download it again
            var song = scenario.LoadSong(before.Id);
            song.AlbumId.ShouldBe(target.Id);
            song.Artists.Select(sa => sa.ArtistId).ShouldBe([artist.Id]);
            song.Checksum.ShouldNotBe(before.Checksum);
            song.FileModifiedAt!.Value.ShouldBeGreaterThan(before.FileModifiedAt!.Value);
            song.RepositoryPath.ShouldBe($"/data/admin/Artist/Album/{before.Title} - Artist.mp3");
            scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeTrue();
            scenario.FileSystem.File.Exists(before.RepositoryPath).ShouldBeFalse();
            song.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
        }

        // Assert: the song the target already had is left alone
        var keptAfter = scenario.LoadSong(kept.Id);
        keptAfter.Checksum.ShouldBe(kept.Checksum);
        keptAfter.RepositoryPath.ShouldBe(kept.RepositoryPath);
        keptAfter.Devices.ShouldAllBe(sd => sd.SyncAction == null);
    }

    [Fact]
    public async Task MergeAsync_AlbumOfAnotherArtist_AddsTheTargetAlbumArtistToTheSongsLackingIt()
    {
        // Arrange
        var scenario = new Scenario();
        var targetArtist = scenario.CreateArtist("Target Artist");
        var sourceArtist = scenario.CreateArtist("Source Artist");
        var target = scenario.CreateAlbum("Album", targetArtist);
        var source = scenario.CreateAlbum("Album", sourceArtist);
        var device = scenario.CreateDevice("Phone");
        var lacking = await scenario.CreateSyncedSongAsync("Lacking", source, device);
        var having = await scenario.CreateSyncedSongAsync("Having", source, device, [targetArtist, sourceArtist]);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: the song gains the target's album artist, after the artists it already had
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([target.Id]);

        var lackingAfter = scenario.LoadSong(lacking.Id);
        lackingAfter.AlbumId.ShouldBe(target.Id);
        lackingAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([sourceArtist.Id, targetArtist.Id]);
        lackingAfter.RepositoryPath.ShouldStartWith("/data/admin/Target Artist/Album/Lacking - ");
        scenario.FileSystem.File.Exists(lackingAfter.RepositoryPath).ShouldBeTrue();
        lackingAfter.Devices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);

        // Assert: a song that already had it keeps its artists as they were
        var havingAfter = scenario.LoadSong(having.Id);
        havingAfter.AlbumId.ShouldBe(target.Id);
        havingAfter.Artists.Select(sa => sa.ArtistId).ShouldBe([targetArtist.Id, sourceArtist.Id]);

        // Assert: the source's album artist still performs the songs, so it stays
        scenario.DbContext.Artists.Select(a => a.Id).ToList()
            .ShouldBe([targetArtist.Id, sourceArtist.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task MergeAsync_SongsLeaveTheirAlbumArtistWithNothing_DeletesThatArtist()
    {
        // Arrange: the source's album artist does not perform its song
        var scenario = new Scenario();
        var targetArtist = scenario.CreateArtist("Target Artist");
        var sourceArtist = scenario.CreateArtist("Source Artist");
        var photo = CreateArtwork(scenario);
        sourceArtist.PhotoId = photo.Id;
        scenario.DbContext.SaveChanges();
        var target = scenario.CreateAlbum("Target", targetArtist);
        var source = scenario.CreateAlbum("Source", sourceArtist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", source, device, [targetArtist]);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: like a song edit, the merge deletes the artist left without songs and albums, and its photo
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([targetArtist.Id]);
        scenario.DbContext.Artworks.Any(a => a.Id == photo.Id).ShouldBeFalse();
        scenario.LoadSong(song.Id).Artists.Select(sa => sa.ArtistId).ShouldBe([targetArtist.Id]);
    }

    [Fact]
    public async Task MergeAsync_EmptyAlbumOfAnotherArtist_DeletesItAndKeepsItsArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var targetArtist = scenario.CreateArtist("Target Artist");
        var sourceArtist = scenario.CreateArtist("Source Artist");
        var target = scenario.CreateAlbum("Target", targetArtist);
        var source = scenario.CreateAlbum("Source", sourceArtist);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert: no song left the artist, which was already empty: it is not the merge's to delete
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([target.Id]);
        scenario.DbContext.Artists.Select(a => a.Id).ToList()
            .ShouldBe([targetArtist.Id, sourceArtist.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task MergeAsync_TargetWithoutCoverAndYear_TakesThemFromTheFirstAlbumThatHasThem()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var bare = scenario.CreateAlbum("Bare", artist);
        var first = scenario.CreateAlbum("First", artist);
        var second = scenario.CreateAlbum("Second", artist);
        var firstCover = CreateArtwork(scenario);
        var secondCover = CreateArtwork(scenario);
        first.CoverId = firstCover.Id;
        second.CoverId = secondCover.Id;
        second.Year = 2002;
        scenario.DbContext.SaveChanges();

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [bare.Id, first.Id, second.Id]);

        // Assert: the cover that was not taken is deleted, since nothing uses it anymore
        scenario.DbContext.ChangeTracker.Clear();
        var merged = scenario.DbContext.Albums.Single();
        merged.Id.ShouldBe(target.Id);
        merged.CoverId.ShouldBe(firstCover.Id);
        merged.Year.ShouldBe(2002);
        scenario.DbContext.Artworks.Select(a => a.Id).ToList().ShouldBe([firstCover.Id]);
    }

    [Fact]
    public async Task MergeAsync_TargetWithCoverAndYear_KeepsItsOwn()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var source = scenario.CreateAlbum("Source", artist);
        var targetCover = CreateArtwork(scenario);
        var sourceCover = CreateArtwork(scenario);
        target.CoverId = targetCover.Id;
        target.Year = 1999;
        source.CoverId = sourceCover.Id;
        source.Year = 2002;
        scenario.DbContext.SaveChanges();

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        var merged = scenario.DbContext.Albums.Single();
        merged.CoverId.ShouldBe(targetCover.Id);
        merged.Year.ShouldBe(1999);
        scenario.DbContext.Artworks.Select(a => a.Id).ToList().ShouldBe([targetCover.Id]);
    }

    [Fact]
    public async Task MergeAsync_AlbumsLinkedToExternalSources_MovesTheLinksTheTargetDoesNotHave()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var first = scenario.CreateAlbum("First", artist);
        var second = scenario.CreateAlbum("Second", artist);
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
        var links = scenario.DbContext.AlbumSources.ToList();
        links.ShouldAllBe(link => link.AlbumId == target.Id);
        links.Select(link => (link.SourceId, link.ExternalId)).ShouldBe(
            [(store.Id, "shared"), (store.Id, "first"), (otherStore.Id, "shared"), (store.Id, "second")],
            ignoreOrder: true);
    }

    [Fact]
    public async Task MergeAsync_IntoAPlaceholderAlbum_MovesTheSongsToIt()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, artist);
        var album = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", album, device);

        // Act
        await CreateService(scenario).MergeAsync(scenario.AdminUser.Id, placeholder.Id, [album.Id]);

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([placeholder.Id]);
        scenario.LoadSong(song.Id).AlbumId.ShouldBe(placeholder.Id);
    }

    [Fact]
    public async Task MergeAsync_PlaceholderAlbumIntoAnother_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, artist);
        var target = scenario.CreateAlbum("Album", artist);
        var device = scenario.CreateDevice("Phone");
        var song = await scenario.CreateSyncedSongAsync("Song", placeholder, device);
        var filesBefore = scenario.ReadMusicFiles();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [placeholder.Id]));

        // Assert
        exception.Message.ShouldBe(
            "The album '(No Album)' of 'Artist' cannot be merged into another album: it holds the songs that have no album");
        scenario.ShouldHaveUnchangedSongs([song], filesBefore);
        scenario.DbContext.Albums.Count().ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_NoAlbumsToMerge_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, []));

        // Assert
        exception.Message.ShouldBe("There are no albums to merge");
    }

    [Fact]
    public async Task MergeAsync_TargetAmongTheAlbumsToMerge_ThrowsAndKeepsEverything()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var source = scenario.CreateAlbum("Source", artist);

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [source.Id, target.Id]));

        // Assert
        exception.Message.ShouldBe("An album cannot be merged into itself");
        scenario.DbContext.Albums.Count().ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_AlbumOfAnotherOwner_ThrowsAlbumNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var mine = scenario.CreateAlbum("Mine", artist);
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var exception = await Should.ThrowAsync<AlbumNotFoundException>(() =>
            CreateService(scenario).MergeAsync(scenario.AdminUser.Id, target.Id, [mine.Id, othersAlbum.Id]));

        // Assert: not even the owner's own album in the list is merged
        exception.Message.ShouldBe($"Album not found with id {othersAlbum.Id}");
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.Albums.Count().ShouldBe(3);
    }

    [Fact]
    public async Task MergeAsync_SecondSongFails_RestoresEveryAlbumAndEverySongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var otherArtist = scenario.CreateArtist("Other Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var firstSource = scenario.CreateAlbum("First Source", otherArtist);
        var secondSource = scenario.CreateAlbum("Second Source", artist);
        var cover = CreateArtwork(scenario);
        firstSource.CoverId = cover.Id;
        firstSource.Year = 2002;
        scenario.DbContext.SaveChanges();
        var device = scenario.CreateDevice("Phone");
        var first = await scenario.CreateSyncedSongAsync("First", firstSource, device);
        var second = await scenario.CreateSyncedSongAsync("Second", secondSource, device);
        var filesBefore = scenario.ReadMusicFiles();
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(scenario.CreateSongFileUpdateService(), second.Id));

        // Act: the first song is moved, gaining an artist, before the second one fails
        await Should.ThrowAsync<IOException>(() =>
            service.MergeAsync(scenario.AdminUser.Id, target.Id, [firstSource.Id, secondSource.Id]));

        // Assert
        scenario.ShouldHaveUnchangedSongs([first, second], filesBefore);
        scenario.DbContext.Albums.Select(a => a.Id).ToList()
            .ShouldBe([target.Id, firstSource.Id, secondSource.Id], ignoreOrder: true);
        var targetAfter = scenario.DbContext.Albums.Single(a => a.Id == target.Id);
        targetAfter.CoverId.ShouldBeNull();
        targetAfter.Year.ShouldBeNull();
    }

    [Fact]
    public async Task PreviewAsync_CountsTheSongsThatMoveAndTheOnesGainingTheTargetAlbumArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var targetArtist = scenario.CreateArtist("Target Artist");
        var sourceArtist = scenario.CreateArtist("Source Artist");
        var target = scenario.CreateAlbum("Target", targetArtist);
        var sameArtist = scenario.CreateAlbum("Same Artist", targetArtist);
        var otherArtist = scenario.CreateAlbum("Other Artist", sourceArtist);
        scenario.CreateSong("Already There", album: target);
        scenario.CreateSong("Same Artist Song", album: sameArtist);
        scenario.CreateSong("Lacking", album: otherArtist);
        scenario.CreateSong("Having", album: otherArtist, artists: [sourceArtist, targetArtist]);

        // Act
        var preview = await CreateService(scenario)
            .PreviewAsync(scenario.AdminUser.Id, target.Id, [sameArtist.Id, otherArtist.Id]);

        // Assert: nothing is changed
        preview.ShouldBe(new AlbumMergePreview(3, 1, "Target Artist"));
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Count().ShouldBe(3);
    }

    [Fact]
    public async Task PreviewAsync_AlbumOfAnotherOwner_ThrowsAlbumNotFoundException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateAlbum("Target", scenario.CreateArtist("Artist"));
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act & Assert
        await Should.ThrowAsync<AlbumNotFoundException>(() =>
            CreateService(scenario).PreviewAsync(scenario.AdminUser.Id, target.Id, [othersAlbum.Id]));
    }
}
