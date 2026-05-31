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

public class PlaylistsControllerQueueFavoritesSpecs
{
    private PlaylistsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        var playlistSongSkipService = new PlaylistSongSkipService();

        return new PlaylistsController(currentUser, playlistSongSkipService);
    }

    #region GetQueue

    [Fact]
    public async Task GetQueue_WhenQueueExists_ReturnsQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue = scenario.CreatePlaylist("Test Queue", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue.Id;
        scenario.DbContext.SaveChanges();

        var result = await controller.GetQueue(scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeNull();
        result.Value.ShouldNotBeNull();
        result.Value.Playlist.Id.ShouldBe(queue.Id);
    }

    [Fact]
    public async Task GetQueue_WhenQueueDoesNotExist_Returns404()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        
        scenario.AdminUser.CurrentQueueId = null;
        scenario.DbContext.SaveChanges();

        var result = await controller.GetQueue(scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundObjectResult>();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task GetQueue_WhenCurrentQueueIdIsNull_Returns404()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        
        scenario.AdminUser.CurrentQueueId = null;
        scenario.DbContext.SaveChanges();

        var result = await controller.GetQueue(scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundObjectResult>();
        result.Value.ShouldBeNull();
    }

    #endregion

    #region GetFavorites

    [Fact]
    public async Task GetFavorites_WhenFavoritesExists_ReturnsFavorites()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var favorites = scenario.CreatePlaylist("Favorites", type: PlaylistType.Favorites);

        var result = await controller.GetFavorites(scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeNull();
        result.Value.ShouldNotBeNull();
        result.Value.Playlist.Id.ShouldBe(favorites.Id);
    }

    [Fact]
    public async Task GetFavorites_WhenFavoritesDoesNotExist_Returns404()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var result = await controller.GetFavorites(scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundObjectResult>();
        result.Value.ShouldBeNull();
    }

    #endregion

    #region AddToQueue Auto-Create

    [Fact]
    public async Task AddToQueue_WhenQueueDoesNotExist_AutoCreatesQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        
        scenario.AdminUser.CurrentQueueId = null;
        scenario.DbContext.SaveChanges();

        var request = new AddToQueueRequest
        {
            SongIds = [song.Id],
            Position = AddToQueuePosition.Last
        };

        var result = await controller.AddToQueue(request, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();

        var queue = await scenario.DbContext.Playlists.FindAsync([user.CurrentQueueId.Value]);
        queue.ShouldNotBeNull();
        queue.Type.ShouldBe(PlaylistType.Queue);
    }

    #endregion

    #region ReplaceQueue Auto-Create

    [Fact]
    public async Task ReplaceQueue_WhenQueueDoesNotExist_AutoCreatesQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        
        scenario.AdminUser.CurrentQueueId = null;
        scenario.DbContext.SaveChanges();

        var request = new ReplaceQueueRequest
        {
            SongIds = [song.Id]
        };

        var result = await controller.ReplaceQueue(request, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();

        var queue = await scenario.DbContext.Playlists.FindAsync([user.CurrentQueueId.Value]);
        queue.ShouldNotBeNull();
        queue.Type.ShouldBe(PlaylistType.Queue);
    }

    #endregion

    #region AddToFavorites Auto-Create

    [Fact]
    public async Task AddToFavorites_WhenFavoritesDoesNotExist_AutoCreatesFavorites()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");

        var request = new AddSongsToPlaylistRequest
        {
            SongIds = [song.Id]
        };

        var result = await controller.AddToFavorites(request, scenario.DbContext, CancellationToken.None);

        var favorites = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Favorites && p.OwnerId == scenario.AdminUser.Id);
        
        favorites.ShouldNotBeNull();
        favorites.Name.ShouldBe("Favorites");
    }

    #endregion
}
