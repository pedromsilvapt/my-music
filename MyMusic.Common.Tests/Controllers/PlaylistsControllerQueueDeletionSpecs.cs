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

    private Song CreateSong(MusicDbContext db, long ownerId, string title)
    {
        var artist = new Artist
        {
            Name = $"{title} Artist",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(artist);
        db.SaveChanges();

        var album = new Album
        {
            Name = $"{title} Album",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(album);
        db.SaveChanges();

        var song = new Song
        {
            Title = title,
            Label = title,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            AlbumId = album.Id,
            Album = album,
            Duration = TimeSpan.FromSeconds(180),
            Size = 5000000,
            RepositoryPath = $"/music/{title}.mp3",
            Checksum = $"checksum-{title}",
            ChecksumAlgorithm = "XxHash128",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = []
        };
        db.Add(song);
        db.SaveChanges();

        var songArtist = new SongArtist
        {
            SongId = song.Id,
            ArtistId = artist.Id,
            Artist = artist,
            Song = song
        };
        db.Add(songArtist);
        db.SaveChanges();

        return song;
    }

    private Playlist CreateQueue(MusicDbContext db, long ownerId, string name)
    {
        var playlist = new Playlist
        {
            Name = name,
            Type = PlaylistType.Queue,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = []
        };
        db.Add(playlist);
        db.SaveChanges();
        return playlist;
    }

    private PlaylistSong AddSongToQueue(MusicDbContext db, Playlist queue, Song song, double order)
    {
        var ps = new PlaylistSong
        {
            PlaylistId = queue.Id,
            SongId = song.Id,
            Order = order,
            AddedAt = DateTime.UtcNow
        };
        db.Add(ps);
        db.SaveChanges();
        return ps;
    }

    #region Delete Non-Current Queue

    [Fact]
    public async Task DeleteQueue_WhenNotCurrentQueue_DeletesWithoutAffectingCurrentQueueId()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue1 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 1");
        var queue2 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 2");
        
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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var queue1 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 1");
        var queue2 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 2");
        AddSongToQueue(scenario.DbContext, queue2, song, 1000);
        
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
        var queue1 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 1");
        var queue2 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 2");
        
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
        var queue1 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 1");
        await Task.Delay(100);
        var queue2 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 2");
        await Task.Delay(100);
        var queue3 = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Queue 3");
        
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
        var queue = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Only Queue");
        
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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var queue = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Only Queue");
        AddSongToQueue(scenario.DbContext, queue, song, 1000);
        
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
        var queue = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Only Queue");
        
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
        var queue = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Only Queue");
        
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
        var queue = CreateQueue(scenario.DbContext, scenario.AdminUser.Id, "Only Queue");
        
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
        var otherQueue = CreateQueue(scenario.DbContext, otherUser.Id, "Other's Queue");

        await Assert.ThrowsAsync<Exception>(() => 
            controller.DeleteQueue(otherQueue.Id, scenario.DbContext, CancellationToken.None));
    }

    #endregion
}
