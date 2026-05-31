using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.PlaylistSongs;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class PlaylistsControllerQueueDeletionSpecs
{
    private PlaylistsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        var playlistSongSkipService = new PlaylistSongSkipService();

        return new PlaylistsController(currentUser, playlistSongSkipService);
    }

    #region Delete Non-Current Queue

    [Fact]
    public async Task DeleteQueue_WhenNotCurrentQueue_DeletesWithoutAffectingCurrentQueueId()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue1 = scenario.CreatePlaylist("Queue 1", type: PlaylistType.Queue);
        var queue2 = scenario.CreatePlaylist("Queue 2", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue1.Id;
        scenario.DbContext.SaveChanges();

        var result = await controller.DeleteQueue(queue2.Id, scenario.DbContext, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldBe(queue1.Id);

        var deletedQueue = await scenario.DbContext.Playlists.FindAsync([queue2.Id]);
        deletedQueue.ShouldBe(null);

        var remainingQueue = await scenario.DbContext.Playlists.FindAsync([queue1.Id]);
        remainingQueue.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteQueue_WhenNotCurrentQueue_RemovesPlaylistSongs()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        var queue1 = scenario.CreatePlaylist("Queue 1", type: PlaylistType.Queue);
        var queue2 = scenario.CreatePlaylist("Queue 2", type: PlaylistType.Queue);
        scenario.AddSongToPlaylist(queue2, song, 1000);;
        
        scenario.AdminUser.CurrentQueueId = queue1.Id;
        scenario.DbContext.SaveChanges();

        await controller.DeleteQueue(queue2.Id, scenario.DbContext, CancellationToken.None);

        var playlistSongs = await scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == queue2.Id)
            .ToListAsync();
        
        playlistSongs.ShouldBeEmpty();
    }

    #endregion

    #region Delete Current Queue - With Replacement

    [Fact]
    public async Task DeleteQueue_WhenCurrentQueueAndOtherQueueExists_SelectsOtherQueueAsCurrent()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue1 = scenario.CreatePlaylist("Queue 1", type: PlaylistType.Queue);
        var queue2 = scenario.CreatePlaylist("Queue 2", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue1.Id;
        scenario.DbContext.SaveChanges();

        await controller.DeleteQueue(queue1.Id, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldBe(queue2.Id);

        var deletedQueue = await scenario.DbContext.Playlists.FindAsync([queue1.Id]);
        deletedQueue.ShouldBe(null);
    }

    [Fact]
    public async Task DeleteQueue_WhenCurrentQueueAndMultipleOtherQueuesExist_SelectsMostRecent()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue1 = scenario.CreatePlaylist("Queue 1", type: PlaylistType.Queue);
        await Task.Delay(100);
        var queue2 = scenario.CreatePlaylist("Queue 2", type: PlaylistType.Queue);
        await Task.Delay(100);
        var queue3 = scenario.CreatePlaylist("Queue 3", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue3.Id;
        scenario.DbContext.SaveChanges();

        await controller.DeleteQueue(queue3.Id, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldBe(queue2.Id);
    }

    #endregion

    #region Delete Last Queue - Auto-Create Replacement

    [Fact]
    public async Task DeleteQueue_WhenLastQueue_AutoCreatesReplacementQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue = scenario.CreatePlaylist("Only Queue", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue.Id;
        scenario.DbContext.SaveChanges();

        await controller.DeleteQueue(queue.Id, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();

        var newQueue = await scenario.DbContext.Playlists.FindAsync([user.CurrentQueueId.Value]);
        newQueue.ShouldNotBeNull();
        newQueue.Type.ShouldBe(PlaylistType.Queue);
    }

    [Fact]
    public async Task DeleteQueue_WhenLastQueue_ReplacementQueueIsEmpty()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = scenario.CreateSong( "Test Song");
        var queue = scenario.CreatePlaylist("Only Queue", type: PlaylistType.Queue);
        scenario.AddSongToPlaylist(queue, song, 1000);;
        
        scenario.AdminUser.CurrentQueueId = queue.Id;
        scenario.DbContext.SaveChanges();

        await controller.DeleteQueue(queue.Id, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();

        var newQueue = await scenario.DbContext.Playlists
            .Include(p => p.PlaylistSongs)
            .FirstAsync(p => p.Id == user.CurrentQueueId.Value);
        
        newQueue.PlaylistSongs.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteQueue_WhenLastQueue_ReplacementQueueHasTimestampBasedName()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue = scenario.CreatePlaylist("Only Queue", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue.Id;
        scenario.DbContext.SaveChanges();

        var now = DateTime.UtcNow;
        await controller.DeleteQueue(queue.Id, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();

        var newQueue = await scenario.DbContext.Playlists.FindAsync([user.CurrentQueueId.Value]);
        newQueue.ShouldNotBeNull();
        newQueue.Name.ShouldBe($"Queue ({now:MMM d, yyyy})");
    }

    [Fact]
    public async Task DeleteQueue_WhenLastQueue_ReplacementQueueBecomesCurrentQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue = scenario.CreatePlaylist("Only Queue", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue.Id;
        scenario.DbContext.SaveChanges();

        var oldQueueId = queue.Id;
        await controller.DeleteQueue(oldQueueId, scenario.DbContext, CancellationToken.None);

        var user = await scenario.DbContext.Users.FindAsync([scenario.AdminUser.Id]);
        user.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBeNull();
        user.CurrentQueueId.ShouldNotBe(oldQueueId);
    }

    [Fact]
    public async Task DeleteQueue_WhenLastQueue_OldQueueIsDeleted()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue = scenario.CreatePlaylist("Only Queue", type: PlaylistType.Queue);
        
        scenario.AdminUser.CurrentQueueId = queue.Id;
        scenario.DbContext.SaveChanges();

        await controller.DeleteQueue(queue.Id, scenario.DbContext, CancellationToken.None);

        var deletedQueue = await scenario.DbContext.Playlists.FindAsync([queue.Id]);
        deletedQueue.ShouldBe(null);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task DeleteQueue_WhenQueueNotFound_ThrowsException()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        await Assert.ThrowsAsync<Exception>(() => 
            controller.DeleteQueue(99999, scenario.DbContext, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteQueue_WhenQueueBelongsToAnotherUser_ThrowsException()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var controller = CreateController(scenario);
        var otherQueue = scenario.CreatePlaylist("Other's Queue", ownerId: otherUser.Id, type: PlaylistType.Queue);

        await Assert.ThrowsAsync<Exception>(() => 
            controller.DeleteQueue(otherQueue.Id, scenario.DbContext, CancellationToken.None));
    }

    #endregion
}
