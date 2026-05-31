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

    [Fact]
    public async Task DeleteAsync_DeletesSongFromDatabase()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);

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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song1 = scenario.CreateSong("Song 1", album: album, repositoryPath: "/data/song1.mp3");
        var song2 = scenario.CreateSong("Song 2", album: album, repositoryPath: "/data/song2.mp3");

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
        var artist = scenario.CreateArtist("Artist", otherUser.Id);
        var album = scenario.CreateAlbum("Album", artist, otherUser.Id);
        var song = scenario.CreateSong("Song", ownerId: otherUser.Id, album: album);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => service.DeleteAsync([song.Id]));
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongArtists()
    {
        // Arrange
        var (service, scenario, currentUser, _) = CreateService();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);

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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var genre = scenario.CreateGenre("Rock");
        var song = scenario.CreateSong("Song", album: album);
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);
        var device = scenario.CreateDevice("Phone", namingTemplate: "/music/{Artist}/{Album}/{Title}");
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);
        var device = scenario.CreateDevice("Phone", namingTemplate: "/music/{Artist}/{Album}/{Title}");
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);
        var playlist = scenario.CreatePlaylist("My Playlist");
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);

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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song1 = scenario.CreateSong("Song 1", album: album, repositoryPath: "/data/song1.mp3");
        scenario.CreateSong("Song 2", album: album, repositoryPath: "/data/song2.mp3");

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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);

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
        var artist = scenario.CreateArtist("Artist");
        var album1 = scenario.CreateAlbum("Album 1", artist);
        var album2 = scenario.CreateAlbum("Album 2", artist);
        var song1 = scenario.CreateSong("Song 1", album: album1, repositoryPath: "/data/song1.mp3");
        scenario.CreateSong("Song 2", album: album2, repositoryPath: "/data/song2.mp3");

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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song1 = scenario.CreateSong("Song 1", album: album, repositoryPath: "/data/song1.mp3");
        var song2 = scenario.CreateSong("Song 2", album: album, repositoryPath: "/data/song2.mp3");

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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var genre = scenario.CreateGenre("Rock");
        var song = scenario.CreateSong("Song", album: album);
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var genre = scenario.CreateGenre("Rock");
        var song1 = scenario.CreateSong("Song 1", album: album, repositoryPath: "/data/song1.mp3");
        var song2 = scenario.CreateSong("Song 2", album: album, repositoryPath: "/data/song2.mp3");
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var artwork = new Artwork { Data = [1, 2, 3], MimeType = "image/jpeg", Width = 100, Height = 100 };
        scenario.DbContext.Add(artwork);
        scenario.DbContext.SaveChanges();
        var song = scenario.CreateSong("Song", album: album);
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var artwork = new Artwork { Data = [1, 2, 3], MimeType = "image/jpeg", Width = 100, Height = 100 };
        scenario.DbContext.Add(artwork);
        scenario.DbContext.SaveChanges();
        var song1 = scenario.CreateSong("Song 1", album: album, repositoryPath: "/data/song1.mp3");
        song1.CoverId = artwork.Id;
        var song2 = scenario.CreateSong("Song 2", album: album, repositoryPath: "/data/song2.mp3");
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
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);
        scenario.DbContext.Add(new PlayHistory { SongId = song.Id, OwnerId = scenario.AdminUser.Id, ClientId = "test", PlayedAt = DateTime.UtcNow });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync([song.Id]);

        // Assert
        scenario.DbContext.PlayHistories.Any(h => h.SongId == song.Id).ShouldBeFalse();
    }
}