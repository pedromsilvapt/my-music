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
            ChecksumAlgorithm = "MD5",
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

    private Playlist CreatePlaylist(MusicDbContext db, long ownerId, string name)
    {
        var playlist = new Playlist
        {
            Name = name,
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

    private PlaylistSong AddSongToPlaylist(MusicDbContext db, Playlist playlist, Song song, double order, bool stopAfterPlayback = false)
    {
        var ps = new PlaylistSong
        {
            PlaylistId = playlist.Id,
            SongId = song.Id,
            Order = order,
            StopAfterPlayback = stopAfterPlayback,
            AddedAt = DateTime.UtcNow
        };
        db.Add(ps);
        db.SaveChanges();
        return ps;
    }

    #region SetStopAfterPlayback

    [Fact]
    public async Task SetStopAfterPlayback_SetFlagToTrue_UpdatesFlag()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, song, 1);

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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, song, 1, stopAfterPlayback: true);

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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Playlist");

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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var otherPlaylist = CreatePlaylist(scenario.DbContext, otherUser.Id, "Other Playlist");
        AddSongToPlaylist(scenario.DbContext, otherPlaylist, song, 1);

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
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1");
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2");
        var song3 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 3");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, song1, 1);
        AddSongToPlaylist(scenario.DbContext, playlist, song2, 2);
        AddSongToPlaylist(scenario.DbContext, playlist, song3, 3);

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
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1");
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, song1, 1, stopAfterPlayback: true);
        AddSongToPlaylist(scenario.DbContext, playlist, song2, 2, stopAfterPlayback: true);

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
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Test Song");
        var otherPlaylist = CreatePlaylist(scenario.DbContext, otherUser.Id, "Other Playlist");
        AddSongToPlaylist(scenario.DbContext, otherPlaylist, song, 1);

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
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1");
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Test Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, song1, 1);
        AddSongToPlaylist(scenario.DbContext, playlist, song2, 2);

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