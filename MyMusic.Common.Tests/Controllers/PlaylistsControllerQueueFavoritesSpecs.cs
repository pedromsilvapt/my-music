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

    private Playlist CreatePlaylist(MusicDbContext db, long ownerId, string name, PlaylistType type = PlaylistType.Playlist)
    {
        var playlist = new Playlist
        {
            Name = name,
            Type = type,
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

    #region GetQueue

    [Fact]
    public async Task GetQueue_WhenQueueExists_ReturnsQueue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var queue = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Queue", PlaylistType.Queue);
        
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
        var favorites = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Favorites", PlaylistType.Favorites);

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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        
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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        
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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");

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
