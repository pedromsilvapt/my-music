using Microsoft.AspNetCore.Mvc;
using MyMusic.Common.Services;
using MyMusic.Common.Services.PlaylistSongs;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Playlists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class PlaylistsControllerSharingSpecs
{
    private static PlaylistsController CreateController(long currentUserId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId);

        return new PlaylistsController(currentUser, new PlaylistSongSkipService());
    }

    [Fact]
    public async Task List_WithoutIncludeShared_ReturnsOnlyOwnPlaylists()
    {
        // Arrange — bob owns a playlist and has one shared with him by the admin
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        scenario.CreatePlaylist("Bob's", bob.Id);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("Admin's"), bob);

        // Act
        var response = await CreateController(bob.Id).List(scenario.DbContext, CancellationToken.None);

        // Assert — the shared playlist is not listed (e.g. the Manage Playlists dialog)
        response.Playlists.Select(p => p.Name).ShouldBe(["Bob's"]);
    }

    [Fact]
    public async Task List_WithIncludeShared_ReturnsSharedPlaylistsFlagged()
    {
        // Arrange — bob owns a playlist and has one shared with him by the admin
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        scenario.CreatePlaylist("Bob's", bob.Id);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("Admin's"), bob);

        // Act
        var response = await CreateController(bob.Id)
            .List(scenario.DbContext, CancellationToken.None, includeShared: true);

        // Assert — the shared playlist is listed with its owner and flagged as shared with me
        var shared = response.Playlists.Single(p => p.Name == "Admin's");
        shared.IsSharedWithMe.ShouldBeTrue();
        shared.OwnerName.ShouldBe("Administrator");
        shared.SharedWithCount.ShouldBe(0);
        response.Playlists.Single(p => p.Name == "Bob's").IsSharedWithMe.ShouldBeFalse();
    }

    [Fact]
    public async Task List_Owner_ReportsSharedWithCount()
    {
        // Arrange — admin shares a playlist with two users
        var scenario = new Scenario();
        var playlist = scenario.CreatePlaylist("Admin's");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, scenario.CreateUser("Bob", "bob"));
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, scenario.CreateUser("Carol", "carol"));

        // Act
        var response = await CreateController(scenario.AdminUser.Id).List(scenario.DbContext, CancellationToken.None);

        // Assert
        var item = response.Playlists.ShouldHaveSingleItem();
        item.SharedWithCount.ShouldBe(2);
        item.IsSharedWithMe.ShouldBeFalse();
    }

    [Fact]
    public async Task List_Recipient_OnlyCountsSongsOwnedByPlaylistOwner()
    {
        // Arrange — admin's shared playlist contains an owned song and a song carol shared with the admin
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var carol = scenario.CreateUser("Carol", "carol");
        var owned = scenario.CreateSong("Owned");
        var carolsSong = scenario.CreateSong("Carol's", ownerId: carol.Id);
        var playlist = scenario.CreatePlaylist("Admin's");
        scenario.AddSongToPlaylist(playlist, owned, 1000);
        scenario.AddSongToPlaylist(playlist, carolsSong, 2000);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, bob);

        // Act
        var response = await CreateController(bob.Id)
            .List(scenario.DbContext, CancellationToken.None, includeShared: true);

        // Assert — only the admin's own song is shared with bob
        var item = response.Playlists.ShouldHaveSingleItem();
        item.SongCount.ShouldBe(1);
        item.SongIds.ShouldBe([owned.Id]);
    }

    [Fact]
    public async Task Get_Recipient_ReturnsOnlySharedSongsFlaggedAsShared()
    {
        // Arrange — same setup: a shared playlist with an owned and a third-party song
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var carol = scenario.CreateUser("Carol", "carol");
        var owned = scenario.CreateSong("Owned");
        var carolsSong = scenario.CreateSong("Carol's", ownerId: carol.Id);
        var playlist = scenario.CreatePlaylist("Admin's");
        scenario.AddSongToPlaylist(playlist, owned, 1000);
        scenario.AddSongToPlaylist(playlist, carolsSong, 2000);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, bob);

        // Act
        var result = await CreateController(bob.Id).Get(playlist.Id, scenario.DbContext, CancellationToken.None);

        // Assert — read-only view with the admin's song only, marked as shared
        var item = result.Value!.Playlist;
        item.IsSharedWithMe.ShouldBeTrue();
        item.OwnerId.ShouldBe(scenario.AdminUser.Id);
        var song = item.Songs.ShouldHaveSingleItem();
        song.Id.ShouldBe(owned.Id);
        song.IsShared.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_Owner_ReturnsAllSongs()
    {
        var scenario = new Scenario();
        var carol = scenario.CreateUser("Carol", "carol");
        var owned = scenario.CreateSong("Owned");
        var carolsSong = scenario.CreateSong("Carol's", ownerId: carol.Id);
        var playlist = scenario.CreatePlaylist("Admin's");
        scenario.AddSongToPlaylist(playlist, owned, 1000);
        scenario.AddSongToPlaylist(playlist, carolsSong, 2000);

        var result = await CreateController(scenario.AdminUser.Id)
            .Get(playlist.Id, scenario.DbContext, CancellationToken.None);

        var item = result.Value!.Playlist;
        item.IsSharedWithMe.ShouldBeFalse();
        item.Songs.Select(s => (s.Id, s.IsShared)).ShouldBe([(owned.Id, false), (carolsSong.Id, true)]);
    }

    [Fact]
    public async Task Get_NotSharedWithMe_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("Admin's");

        var result = await CreateController(bob.Id).Get(playlist.Id, scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Mutations_AsRecipient_AreRejected()
    {
        // Arrange — playlist shared with bob
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var song = scenario.CreateSong("Song");
        var playlist = scenario.CreatePlaylist("Admin's");
        scenario.AddSongToPlaylist(playlist, song, 1000);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, bob);
        var controller = CreateController(bob.Id);

        // Act & Assert — shared playlists are read-only for recipients
        await Should.ThrowAsync<Exception>(() =>
            controller.Update(playlist.Id, new UpdatePlaylistRequest { Name = "Hijacked" }, scenario.DbContext,
                CancellationToken.None));
        await Should.ThrowAsync<Exception>(() =>
            controller.Delete(playlist.Id, scenario.DbContext, CancellationToken.None));
        await Should.ThrowAsync<Exception>(() =>
            controller.AddSongs(playlist.Id, new AddSongsToPlaylistRequest { SongIds = [song.Id] },
                scenario.DbContext, CancellationToken.None));
        await Should.ThrowAsync<Exception>(() =>
            controller.RemoveSong(playlist.Id, song.Id, scenario.DbContext, CancellationToken.None));
    }
}
