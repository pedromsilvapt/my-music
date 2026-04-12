using System.IO.Abstractions;
using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SongUpdateServiceSpecs
{
    private SongUpdateService CreateService(IFileSystem fileSystem)
    {
        return new SongUpdateService(
            fileSystem,
            Options.Create(new Config { MusicRepositoryPath = "/data" }),
            Substitute.For<ILogger<SongUpdateService>>());
    }

    private (string checksum, string algorithm) SetupMusicFile(IFileSystem fileSystem, string repositoryPath, string ownerUsername)
    {
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(repositoryPath)!);
        fileSystem.Directory.CreateDirectory(fileSystem.Path.Join("/data", ownerUsername));
        MockMusicFile.Create(fileSystem, repositoryPath, "My Song", "My Song Album", ["My Song Artist"], ["Rock"]);
        var algo = new XxHash128();
        var checksum = ChecksumService.CalculateChecksum(fileSystem, algo, repositoryPath);
        return (checksum, algo.GetType().Name);
    }

    [Fact]
    public async Task UpdateSong_SongOnDevice_SetsSyncActionToDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SongOnMultipleDevices_SetsSyncActionToDownloadForAll()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device1 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var device2 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Tablet");
        AddSongToDevice(scenario.DbContext, song, device1, "/music/My Song.mp3");
        AddSongToDevice(scenario.DbContext, song, device2, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == song.Id)
            .ToList();
        songDevices.Count.ShouldBe(2);
        songDevices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SongNotOnAnyDevice_DoesNotCreateSongDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        scenario.DbContext.SongDevices.Count().ShouldBe(0);
    }

    [Fact]
    public async Task UpdateSong_SongDevicePendingRemove_DoesNotChangeSyncAction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: SongSyncAction.Remove);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task UpdateSong_SongDeviceAlreadyPendingDownload_StaysDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: SongSyncAction.Download);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SyncedSongDevice_GetsMarkedForDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: null, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));

        var update = new SongUpdateModel { Lyrics = new ValueUpdate<string>("New lyrics") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task BatchUpdateSong_SongOnDevice_SetsSyncActionToDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        var result = await service.BatchUpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Success.ShouldBeTrue();
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SameFileContent_DoesNotSetSyncAction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: null, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));

        var firstUpdate = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };
        await service.UpdateSong(scenario.DbContext, song.Id, firstUpdate);

        var songAfterFirst = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        var checksumAfterFirst = songAfterFirst.Checksum;

        var songDeviceAfterFirst = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDeviceAfterFirst.SyncAction.ShouldBe(SongSyncAction.Download);
        songDeviceAfterFirst.SyncAction = null;
        scenario.DbContext.SaveChanges();

        var secondUpdate = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, secondUpdate);

        // Assert
        var songAfterSecond = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        songAfterSecond.Checksum.ShouldBe(checksumAfterFirst);

        var songDeviceAfterSecond = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDeviceAfterSecond.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateSong_MixedDevices_OnlyNonRemoveDevicesGetDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "My Song", checksum, algo);
        var device1 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var device2 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Tablet");
        AddSongToDevice(scenario.DbContext, song, device1, "/music/My Song.mp3",
            syncAction: null, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));
        AddSongToDevice(scenario.DbContext, song, device2, "/music/My Song.mp3",
            syncAction: SongSyncAction.Remove);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == song.Id)
            .ToList();
        songDevices.Count.ShouldBe(2);
        songDevices.First(sd => sd.DeviceId == device1.Id).SyncAction.ShouldBe(SongSyncAction.Download);
        songDevices.First(sd => sd.DeviceId == device2.Id).SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    #region Helpers

    private Song CreateSong(MusicDbContext db, long ownerId, string title, string checksum, string checksumAlgorithm)
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
            RepositoryPath = $"/data/{title}.mp3",
            Checksum = checksum,
            ChecksumAlgorithm = checksumAlgorithm,
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

    private Device CreateDevice(MusicDbContext db, long ownerId, string name)
    {
        var device = new Device
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            Songs = []
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private void AddSongToDevice(MusicDbContext db, Song song, Device device, string path,
        SongSyncAction? syncAction = null, DateTime? lastSyncedModifiedAt = null)
    {
        var sd = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = path,
            AddedAt = DateTime.UtcNow,
            SyncAction = syncAction,
            LastSyncedModifiedAt = lastSyncedModifiedAt,
        };
        db.Add(sd);
        db.SaveChanges();
    }

    #endregion
}
