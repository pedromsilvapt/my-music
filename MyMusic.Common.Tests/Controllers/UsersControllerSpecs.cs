using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Users;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class UsersControllerSpecs
{
    private UsersController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new UsersController(
            Substitute.For<ILogger<UsersController>>(),
            currentUser);
    }

    #region User Creation with Playlists

    [Fact]
    public async Task Create_CreatesQueuePlaylist()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "testuser",
                Name = "Test User"
            }
        };

        await controller.Create(scenario.DbContext, request, CancellationToken.None);

        var queue = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Queue && p.Owner.Username == "testuser");
        
        queue.ShouldNotBeNull();
        queue.Name.ShouldStartWith("Queue (");
    }

    [Fact]
    public async Task Create_CreatesFavoritesPlaylist()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "testuser",
                Name = "Test User"
            }
        };

        await controller.Create(scenario.DbContext, request, CancellationToken.None);

        var favorites = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Favorites && p.Owner.Username == "testuser");
        
        favorites.ShouldNotBeNull();
        favorites.Name.ShouldBe("Favorites");
    }

    [Fact]
    public async Task Create_SetsCurrentQueueIdToNewQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "testuser",
                Name = "Test User"
            }
        };

        await controller.Create(scenario.DbContext, request, CancellationToken.None);

        var user = await scenario.DbContext.Users
            .FirstOrDefaultAsync(u => u.Username == "testuser");
        
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();
        
        var queue = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Id == user.CurrentQueueId.Value);
        
        queue.ShouldNotBeNull();
        queue.Type.ShouldBe(PlaylistType.Queue);
    }

    [Fact]
    public async Task Create_CreatesEmptyPlaylists()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "testuser",
                Name = "Test User"
            }
        };

        await controller.Create(scenario.DbContext, request, CancellationToken.None);

        var queue = await scenario.DbContext.Playlists
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Queue && p.Owner.Username == "testuser");
        
        queue.ShouldNotBeNull();
        queue.PlaylistSongs.ShouldBeEmpty();

        var favorites = await scenario.DbContext.Playlists
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Favorites && p.Owner.Username == "testuser");
        
        favorites.ShouldNotBeNull();
        favorites.PlaylistSongs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_CreatesPlaylistsWithCorrectOwnerId()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "testuser",
                Name = "Test User"
            }
        };

        var response = await controller.Create(scenario.DbContext, request, CancellationToken.None);
        var userId = response.User.Id;

        var queue = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Queue && p.OwnerId == userId);
        
        queue.ShouldNotBeNull();
        queue.OwnerId.ShouldBe(userId);

        var favorites = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Favorites && p.OwnerId == userId);
        
        favorites.ShouldNotBeNull();
        favorites.OwnerId.ShouldBe(userId);
    }

    [Fact]
    public async Task Create_UsesTimestampBasedQueueName()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "testuser",
                Name = "Test User"
            }
        };

        var now = DateTime.UtcNow;
        await controller.Create(scenario.DbContext, request, CancellationToken.None);

        var queue = await scenario.DbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.Queue && p.Owner.Username == "testuser");
        
        queue.ShouldNotBeNull();
        queue.Name.ShouldBe($"Queue ({now:MMM d, yyyy})");
    }

    #endregion
}
