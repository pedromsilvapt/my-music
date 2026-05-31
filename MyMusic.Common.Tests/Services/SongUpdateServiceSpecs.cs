using System.IO.Abstractions;
using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device1 = scenario.CreateDevice("Phone");
        var device2 = scenario.CreateDevice("Tablet");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");

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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
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
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device1 = scenario.CreateDevice("Phone");
        var device2 = scenario.CreateDevice("Tablet");
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

    [Fact]
    public async Task UpdateSong_ChangeArtists_SetsSyncActionToDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario.FileSystem);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var newArtist = new Artist
        {
            Name = "New Artist",
            OwnerId = scenario.AdminUser.Id,
            Owner = scenario.AdminUser,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        scenario.DbContext.Add(newArtist);
        scenario.DbContext.SaveChanges();

        // Include both the new artist and the album artist (required by validation)
        var albumArtist = song.Album.Artist;
        var update = new SongUpdateModel
        {
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(newArtist.Id, null), new ArtistRef(albumArtist.Id, null)])
        };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);

        var updatedSong = scenario.DbContext.Songs
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .First(s => s.Id == song.Id);
        updatedSong.Artists.Count.ShouldBe(2);
        updatedSong.Artists.Select(a => a.Artist.Name).ShouldContain("New Artist");
    }

    #region Helpers

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
