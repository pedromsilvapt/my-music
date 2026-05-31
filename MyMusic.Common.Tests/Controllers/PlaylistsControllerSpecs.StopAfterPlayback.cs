using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.PlaylistSongs;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Playlists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class PlaylistsControllerStopAfterPlaybackSpecs
{
    private PlaylistsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var playlistSongSkipService = new PlaylistSongSkipService();

        return new PlaylistsController(currentUser, playlistSongSkipService);
    }

    #region SetStopAfterPlayback

    [Fact]
    public async Task SetStopAfterPlayback_SetFlagToTrue_UpdatesFlag()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song, 1);

        // Act
        var result = await controller.SetStopAfterPlayback(
            playlist.Id, song.Id,
            new SetStopAfterPlaybackRequest { StopAfterPlayback = true },
            scenario.DbContext, CancellationToken.None);

        // Assert
        var ps = await scenario.DbContext.PlaylistSongs.FirstAsync();
        ps.StopAfterPlayback.ShouldBeTrue();
        result.Playlist.Songs[0].StopAfterPlayback.ShouldBeTrue();
    }

    [Fact]
    public async Task SetStopAfterPlayback_SetFlagToFalse_ClearsFlag()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song, 1, stopAfterPlayback: true);

        // Act
        var result = await controller.SetStopAfterPlayback(
            playlist.Id, song.Id,
            new SetStopAfterPlaybackRequest { StopAfterPlayback = false },
            scenario.DbContext, CancellationToken.None);

        // Assert
        var ps = await scenario.DbContext.PlaylistSongs.FirstAsync();
        ps.StopAfterPlayback.ShouldBeFalse();
        result.Playlist.Songs[0].StopAfterPlayback.ShouldBeFalse();
    }

    [Fact]
    public async Task SetStopAfterPlayback_SongNotInPlaylist_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.SetStopAfterPlayback(
                playlist.Id, song.Id,
                new SetStopAfterPlaybackRequest { StopAfterPlayback = true },
                scenario.DbContext, CancellationToken.None));
    }

    [Fact]
    public async Task SetStopAfterPlayback_AnotherUsersPlaylist_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong( "Test Song");
        var otherPlaylist = scenario.CreatePlaylist("Other Playlist", ownerId: otherUser.Id);
        scenario.AddSongToPlaylist(otherPlaylist, song, 1);

        var controller = CreateController(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.SetStopAfterPlayback(
                otherPlaylist.Id, song.Id,
                new SetStopAfterPlaybackRequest { StopAfterPlayback = true },
                scenario.DbContext, CancellationToken.None));
    }

    #endregion

    #region BatchSetStopAfterPlayback

    [Fact]
    public async Task BatchSetStopAfterPlayback_SetsFlagOnMultipleSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong( "Song 1");
        var song2 = scenario.CreateSong( "Song 2");
        var song3 = scenario.CreateSong( "Song 3");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1);
        scenario.AddSongToPlaylist(playlist, song2, 2);
        scenario.AddSongToPlaylist(playlist, song3, 3);

        // Act
        var result = await controller.BatchSetStopAfterPlayback(
            new BatchSetStopAfterPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id, song3.Id],
                StopAfterPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        // Assert - response DTOs have correct flags
        result.Playlist.Songs.First(s => s.Id == song1.Id).StopAfterPlayback.ShouldBeTrue();
        result.Playlist.Songs.First(s => s.Id == song2.Id).StopAfterPlayback.ShouldBeFalse();
        result.Playlist.Songs.First(s => s.Id == song3.Id).StopAfterPlayback.ShouldBeTrue();
    }

    [Fact]
    public async Task BatchSetStopAfterPlayback_ClearsFlagOnMultipleSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong( "Song 1");
        var song2 = scenario.CreateSong( "Song 2");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1, stopAfterPlayback: true);
        scenario.AddSongToPlaylist(playlist, song2, 2, stopAfterPlayback: true);

        // Act
        var result = await controller.BatchSetStopAfterPlayback(
            new BatchSetStopAfterPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id, song2.Id],
                StopAfterPlayback = false
            },
            scenario.DbContext, CancellationToken.None);

        // Assert - response DTOs reflect cleared flags
        result.Playlist.Songs.First(s => s.Id == song1.Id).StopAfterPlayback.ShouldBeFalse();
        result.Playlist.Songs.First(s => s.Id == song2.Id).StopAfterPlayback.ShouldBeFalse();
    }

    [Fact]
    public async Task BatchSetStopAfterPlayback_AnotherUsersPlaylist_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong( "Test Song");
        var otherPlaylist = scenario.CreatePlaylist("Other Playlist", ownerId: otherUser.Id);
        scenario.AddSongToPlaylist(otherPlaylist, song, 1);

        var controller = CreateController(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.BatchSetStopAfterPlayback(
                new BatchSetStopAfterPlaybackRequest
                {
                    PlaylistId = otherPlaylist.Id,
                    SongIds = [song.Id],
                    StopAfterPlayback = true
                },
                scenario.DbContext, CancellationToken.None));
    }

    [Fact]
    public async Task BatchSetStopAfterPlayback_NonExistentPlaylist_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.BatchSetStopAfterPlayback(
                new BatchSetStopAfterPlaybackRequest
                {
                    PlaylistId = 99999,
                    SongIds = [1],
                    StopAfterPlayback = true
                },
                scenario.DbContext, CancellationToken.None));
    }

    [Fact]
    public async Task BatchSetStopAfterPlayback_OnlyAffectsSpecifiedSongsInPlaylist()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong( "Song 1");
        var song2 = scenario.CreateSong( "Song 2");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1);
        scenario.AddSongToPlaylist(playlist, song2, 2);

        // Act - batch set only song1
        var result = await controller.BatchSetStopAfterPlayback(
            new BatchSetStopAfterPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id],
                StopAfterPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        // Assert - song1 has flag, song2 does not
        result.Playlist.Songs.First(s => s.Id == song1.Id).StopAfterPlayback.ShouldBeTrue();
        result.Playlist.Songs.First(s => s.Id == song2.Id).StopAfterPlayback.ShouldBeFalse();
    }

    #endregion
}