using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SongDeleteServiceSpecs
{
    private (SongDeleteService service, Scenario scenario, ICurrentUser currentUser, IFileSystem fileSystem) CreateService()
    {
        var scenario = new Scenario();
        var currentUser = Substitute.For<ICurrentUser>();
        var fileSystem = Scenario.CreateFileSystem();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        var albumDeleteLogger = Substitute.For<ILogger<AlbumDeleteService>>();
        var artistDeleteLogger = Substitute.For<ILogger<ArtistDeleteService>>();
        var genreDeleteLogger = Substitute.For<ILogger<GenreDeleteService>>();
        var artworkDeleteLogger = Substitute.For<ILogger<ArtworkDeleteService>>();
        var songDeleteLogger = Substitute.For<ILogger<SongDeleteService>>();

        var albumDeleteService = new AlbumDeleteService(scenario.DbContext, albumDeleteLogger);
        var artistDeleteService = new ArtistDeleteService(scenario.DbContext, artistDeleteLogger);
        var genreDeleteService = new GenreDeleteService(scenario.DbContext, genreDeleteLogger);
        var artworkDeleteService = new ArtworkDeleteService(scenario.DbContext, artworkDeleteLogger);

        var service = new SongDeleteService(
            scenario.DbContext,
            currentUser,
            fileSystem,
            config,
            albumDeleteService,
            artistDeleteService,
            genreDeleteService,
            artworkDeleteService,
            songDeleteLogger);

        return (service, scenario, currentUser, fileSystem);
    }

    private Artist CreateArtist(MusicDbContext db, long ownerId, string name)
    {
        var artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(artist);
        db.SaveChanges();
        return artist;
    }

    private Album CreateAlbum(MusicDbContext db, long ownerId, string name, Artist artist)
    {
        var album = new Album
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(album);
        db.SaveChanges();
        return album;
    }

    private Song CreateSong(MusicDbContext db, long ownerId, string title, Album album, string repositoryPath = "/data/song.mp3")
    {
        var song = new Song
        {
            Title = title,
            Label = title,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            AlbumId = album.Id,
            Album = album,
            Duration = TimeSpan.FromMinutes(3),
            Size = 3000000,
            RepositoryPath = repositoryPath,
            Checksum = "abc123",
            ChecksumAlgorithm = "XxHash128",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Add(song);
        db.SaveChanges();
        return song;
    }

    private Genre CreateGenre(MusicDbContext db, long ownerId, string name)
    {
        var genre = new Genre
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
        };
        db.Add(genre);
        db.SaveChanges();
        return genre;
    }

    private Device CreateDevice(MusicDbContext db, long ownerId, string name)
    {
        var device = new Device
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            NamingTemplate = "/music/{Artist}/{Album}/{Title}",
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private Playlist CreatePlaylist(MusicDbContext db, long ownerId, string name)
    {
        var playlist = new Playlist
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            PlaylistSongs = [],
        };
        db.Add(playlist);
        db.SaveChanges();
        return playlist;
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongFromDatabase()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);

        // Act
        var result = await service.DeleteAsync([song.Id]);

        // Assert
        result.ShouldBe(1);
        scenario.DbContext.Songs.Any(s => s.Id == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesMultipleSongs()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1", album, "/data/song1.mp3");
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2", album, "/data/song2.mp3");

        // Act
        var result = await service.DeleteAsync([song1.Id, song2.Id]);

        // Assert
        result.ShouldBe(2);
        scenario.DbContext.Songs.Any(s => s.Id == song1.Id || s.Id == song2.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenSongNotFound()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => service.DeleteAsync([999999]));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenSongNotOwnedByUser()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        var otherUser = scenario.CreateUser("Other", "other");
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, otherUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, otherUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, otherUser.Id, "Song", album);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => service.DeleteAsync([song.Id]));
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongArtists()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        scenario.DbContext.Add(new SongArtist { SongId = song.Id, ArtistId = artist.Id });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.SongArtists.Any(sa => sa.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongGenres()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var genre = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Rock");
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        scenario.DbContext.Add(new SongGenre { SongId = song.Id, GenreId = genre.Id });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.SongGenres.Any(sg => sg.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NullsSongDeviceSongIdAndSetsRemoveSyncAction()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            SyncAction = null,
            AddedAt = DateTime.UtcNow,
        };
        scenario.DbContext.Add(songDevice);
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        var sd = scenario.DbContext.SongDevices.First(sd => sd.Id == songDevice.Id);
        sd.SongId.ShouldBeNull();
        sd.SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task DeleteAsync_DeletesDownloadSyncActionSongDevices()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            SyncAction = SongSyncAction.Download,
            AddedAt = DateTime.UtcNow,
        };
        scenario.DbContext.Add(songDevice);
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.SongDevices.Any(sd => sd.Id == songDevice.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesPlaylistSongs()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "My Playlist");
        scenario.DbContext.Add(new PlaylistSong { SongId = song.Id, PlaylistId = playlist.Id, Order = 1, AddedAt = DateTime.UtcNow });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.PlaylistSongs.Any(ps => ps.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesOrphanedAlbum()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.Albums.Any(a => a.Id == album.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_KeepsAlbumWithOtherSongs()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1", album, "/data/song1.mp3");
        CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2", album, "/data/song2.mp3");

        // Act
        await service.DeleteAsync([song1.Id]);

        // Assert
        scenario.DbContext.Albums.Any(a => a.Id == album.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_DeletesOrphanedArtist()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        scenario.DbContext.Add(new SongArtist { SongId = song.Id, ArtistId = artist.Id });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.Artists.Any(a => a.Id == artist.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_KeepsArtistWithOtherAlbums()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album1 = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album 1", artist);
        var album2 = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album 2", artist);
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1", album1, "/data/song1.mp3");
        CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2", album2, "/data/song2.mp3");

        // Act
        await service.DeleteAsync([song1.Id]);

        // Assert
        scenario.DbContext.Artists.Any(a => a.Id == artist.Id).ShouldBeTrue();
        scenario.DbContext.Albums.Any(a => a.Id == album2.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_KeepsArtistWithOtherSongs()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1", album, "/data/song1.mp3");
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2", album, "/data/song2.mp3");
        scenario.DbContext.Add(new SongArtist { SongId = song1.Id, ArtistId = artist.Id });
        scenario.DbContext.Add(new SongArtist { SongId = song2.Id, ArtistId = artist.Id });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song1.Id]);

        // Assert
        scenario.DbContext.Artists.Any(a => a.Id == artist.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_DeletesOrphanedGenre()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var genre = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Rock");
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        scenario.DbContext.Add(new SongGenre { SongId = song.Id, GenreId = genre.Id });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.Genres.Any(g => g.Id == genre.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_KeepsGenreWithOtherSongs()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var genre = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Rock");
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1", album, "/data/song1.mp3");
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2", album, "/data/song2.mp3");
        scenario.DbContext.Add(new SongGenre { SongId = song1.Id, GenreId = genre.Id });
        scenario.DbContext.Add(new SongGenre { SongId = song2.Id, GenreId = genre.Id });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song1.Id]);

        // Assert
        scenario.DbContext.Genres.Any(g => g.Id == genre.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_DeletesOrphanedSongArtwork()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var artwork = new Artwork { Data = [1, 2, 3], MimeType = "image/jpeg", Width = 100, Height = 100 };
        scenario.DbContext.Add(artwork);
        scenario.DbContext.SaveChanges();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        song.CoverId = artwork.Id;
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.Artworks.Any(a => a.Id == artwork.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_KeepsArtworkReferencedByOtherSongs()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var artwork = new Artwork { Data = [1, 2, 3], MimeType = "image/jpeg", Width = 100, Height = 100 };
        scenario.DbContext.Add(artwork);
        scenario.DbContext.SaveChanges();
        var song1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 1", album, "/data/song1.mp3");
        song1.CoverId = artwork.Id;
        var song2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song 2", album, "/data/song2.mp3");
        song2.CoverId = artwork.Id;
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song1.Id]);

        // Assert
        scenario.DbContext.Artworks.Any(a => a.Id == artwork.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_DeletesPlayHistory()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist");
        var album = CreateAlbum(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Song", album);
        scenario.DbContext.Add(new PlayHistory { SongId = song.Id, OwnerId = scenario.AdminUser.Id, ClientId = "test", PlayedAt = DateTime.UtcNow });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.PlayHistories.Any(h => h.SongId == song.Id).ShouldBeFalse();
    }
}