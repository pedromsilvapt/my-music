using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.PlaylistSongs;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Playlists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class PlaylistsControllerSkipNextPlaybackSpecs
{
    private PlaylistsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var playlistSongSkipService = new PlaylistSongSkipService();

        return new PlaylistsController(currentUser, playlistSongSkipService);
    }

    #region SetSkipNextPlayback

    [Fact]
    public async Task SetSkipNextPlayback_SetFlagToTrue_UpdatesFlag()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song, 1);

        var result = await controller.SetSkipNextPlayback(
            playlist.Id, song.Id,
            new SetSkipNextPlaybackRequest { SkipNextPlayback = true },
            scenario.DbContext, CancellationToken.None);

        var ps = await scenario.DbContext.PlaylistSongs.FirstAsync();
        ps.SkipNextPlayback.ShouldBeTrue();
        result.Value!.Playlist.Songs[0].SkipNextPlayback.ShouldBeTrue();
    }

    [Fact]
    public async Task SetSkipNextPlayback_SetFlagToFalse_ClearsFlag()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song, 1, skipNextPlayback: true);

        var result = await controller.SetSkipNextPlayback(
            playlist.Id, song.Id,
            new SetSkipNextPlaybackRequest { SkipNextPlayback = false },
            scenario.DbContext, CancellationToken.None);

        var ps = await scenario.DbContext.PlaylistSongs.FirstAsync();
        ps.SkipNextPlayback.ShouldBeFalse();
        result.Value!.Playlist.Songs[0].SkipNextPlayback.ShouldBeFalse();
    }

    [Fact]
    public async Task SetSkipNextPlayback_SongNotInPlaylist_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");

        var result = await controller.SetSkipNextPlayback(
            playlist.Id, song.Id,
            new SetSkipNextPlaybackRequest { SkipNextPlayback = true },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetSkipNextPlayback_AnotherUsersPlaylist_ReturnsForbid()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Test Song");
        var otherPlaylist = scenario.CreatePlaylist("Other Playlist", ownerId: otherUser.Id);
        scenario.AddSongToPlaylist(otherPlaylist, song, 1);

        var controller = CreateController(scenario);

        var result = await controller.SetSkipNextPlayback(
            otherPlaylist.Id, song.Id,
            new SetSkipNextPlaybackRequest { SkipNextPlayback = true },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<ForbidResult>();
    }

    #endregion

    #region BatchSetSkipNextPlayback

    [Fact]
    public async Task BatchSetSkipNextPlayback_SetsFlagOnMultipleSongs()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");
        var song3 = scenario.CreateSong("Song 3");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1);
        scenario.AddSongToPlaylist(playlist, song2, 2);
        scenario.AddSongToPlaylist(playlist, song3, 3);

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id, song3.Id],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Value!.Playlist.Songs.First(s => s.Id == song1.Id).SkipNextPlayback.ShouldBeTrue();
        result.Value!.Playlist.Songs.First(s => s.Id == song2.Id).SkipNextPlayback.ShouldBeFalse();
        result.Value!.Playlist.Songs.First(s => s.Id == song3.Id).SkipNextPlayback.ShouldBeTrue();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_ClearsFlagOnMultipleSongs()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1, skipNextPlayback: true);
        scenario.AddSongToPlaylist(playlist, song2, 2, skipNextPlayback: true);

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id, song2.Id],
                SkipNextPlayback = false
            },
            scenario.DbContext, CancellationToken.None);

        result.Value!.Playlist.Songs.First(s => s.Id == song1.Id).SkipNextPlayback.ShouldBeFalse();
        result.Value!.Playlist.Songs.First(s => s.Id == song2.Id).SkipNextPlayback.ShouldBeFalse();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_AnotherUsersPlaylist_ReturnsForbid()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Test Song");
        var otherPlaylist = scenario.CreatePlaylist("Other Playlist", ownerId: otherUser.Id);
        scenario.AddSongToPlaylist(otherPlaylist, song, 1);

        var controller = CreateController(scenario);

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = otherPlaylist.Id,
                SongIds = [song.Id],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_NonExistentPlaylist_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = 99999,
                SongIds = [1],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_OnlyAffectsSpecifiedSongsInPlaylist()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1);
        scenario.AddSongToPlaylist(playlist, song2, 2);

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Value!.Playlist.Songs.First(s => s.Id == song1.Id).SkipNextPlayback.ShouldBeTrue();
        result.Value!.Playlist.Songs.First(s => s.Id == song2.Id).SkipNextPlayback.ShouldBeFalse();
    }

    #endregion

    #region MutualExclusivity

    [Fact]
    public async Task SetSkipNextPlayback_SetTrue_ClearsStopAfterPlayback()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        var ps = scenario.AddSongToPlaylist(playlist, song, 1);
        ps.StopAfterPlayback = true;
        scenario.DbContext.SaveChanges();

        var result = await controller.SetSkipNextPlayback(
            playlist.Id, song.Id,
            new SetSkipNextPlaybackRequest { SkipNextPlayback = true },
            scenario.DbContext, CancellationToken.None);

        var updatedPs = await scenario.DbContext.PlaylistSongs.FirstAsync();
        updatedPs.SkipNextPlayback.ShouldBeTrue();
        updatedPs.StopAfterPlayback.ShouldBeFalse();
    }

    [Fact]
    public async Task SetSkipNextPlayback_SetFalse_DoesNotAffectStopAfterPlayback()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        var ps = scenario.AddSongToPlaylist(playlist, song, 1);
        ps.StopAfterPlayback = true;
        scenario.DbContext.SaveChanges();

        var result = await controller.SetSkipNextPlayback(
            playlist.Id, song.Id,
            new SetSkipNextPlaybackRequest { SkipNextPlayback = false },
            scenario.DbContext, CancellationToken.None);

        var updatedPs = await scenario.DbContext.PlaylistSongs.FirstAsync();
        updatedPs.SkipNextPlayback.ShouldBeFalse();
        updatedPs.StopAfterPlayback.ShouldBeTrue();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_SetTrue_ClearsStopAfterPlaybackOnAllSongs()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        var ps1 = scenario.AddSongToPlaylist(playlist, song1, 1);
        var ps2 = scenario.AddSongToPlaylist(playlist, song2, 2);
        ps1.StopAfterPlayback = true;
        ps2.StopAfterPlayback = true;
        scenario.DbContext.SaveChanges();

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id, song2.Id],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        var allPs = await scenario.DbContext.PlaylistSongs.ToListAsync();
        allPs.First(s => s.SongId == song1.Id).StopAfterPlayback.ShouldBeFalse();
        allPs.First(s => s.SongId == song2.Id).StopAfterPlayback.ShouldBeFalse();
        allPs.First(s => s.SongId == song1.Id).SkipNextPlayback.ShouldBeTrue();
        allPs.First(s => s.SongId == song2.Id).SkipNextPlayback.ShouldBeTrue();
    }

    [Fact]
    public async Task SetStopAfterPlayback_SetTrue_ClearsSkipNextPlayback()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        var ps = scenario.AddSongToPlaylist(playlist, song, 1);
        ps.SkipNextPlayback = true;
        scenario.DbContext.SaveChanges();

        var result = await controller.SetStopAfterPlayback(
            playlist.Id, song.Id,
            new SetStopAfterPlaybackRequest { StopAfterPlayback = true },
            scenario.DbContext, CancellationToken.None);

        var updatedPs = await scenario.DbContext.PlaylistSongs.FirstAsync();
        updatedPs.StopAfterPlayback.ShouldBeTrue();
        updatedPs.SkipNextPlayback.ShouldBeFalse();
    }

    [Fact]
    public async Task SetStopAfterPlayback_SetFalse_DoesNotAffectSkipNextPlayback()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        var ps = scenario.AddSongToPlaylist(playlist, song, 1);
        ps.SkipNextPlayback = true;
        scenario.DbContext.SaveChanges();

        var result = await controller.SetStopAfterPlayback(
            playlist.Id, song.Id,
            new SetStopAfterPlaybackRequest { StopAfterPlayback = false },
            scenario.DbContext, CancellationToken.None);

        var updatedPs = await scenario.DbContext.PlaylistSongs.FirstAsync();
        updatedPs.StopAfterPlayback.ShouldBeFalse();
        updatedPs.SkipNextPlayback.ShouldBeTrue();
    }

    #endregion

    #region BatchValidation

    [Fact]
    public async Task BatchSetSkipNextPlayback_EmptySongIds_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var playlist = scenario.CreatePlaylist("Test Playlist");

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_SongIdsNotInPlaylist_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong("Test Song");
        var playlist = scenario.CreatePlaylist("Test Playlist");

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song.Id],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BatchSetSkipNextPlayback_PartiallyInvalidSongIds_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");
        var playlist = scenario.CreatePlaylist("Test Playlist");
        scenario.AddSongToPlaylist(playlist, song1, 1);

        var result = await controller.BatchSetSkipNextPlayback(
            new BatchSetSkipNextPlaybackRequest
            {
                PlaylistId = playlist.Id,
                SongIds = [song1.Id, song2.Id],
                SkipNextPlayback = true
            },
            scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    #endregion
}
