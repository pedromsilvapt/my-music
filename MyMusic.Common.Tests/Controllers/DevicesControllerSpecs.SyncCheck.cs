using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerSyncCheckSpecs
{
    private DevicesController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<IMusicService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            Substitute.For<ILogger<MusicImportJob>>(),
            Substitute.For<System.IO.Abstractions.IFileSystem>()
        );
    }

    private Device CreateDevice(MusicDbContext db, long ownerId)
    {
        var device = new Device
        {
            Name = $"Device-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            Songs = []
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private Song CreateSong(MusicDbContext db, long ownerId, DateTime modifiedAt, string checksum = "AA==", string checksumAlgorithm = "MD5")
    {
        var artist = new Artist
        {
            Name = $"Artist-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(artist);
        db.SaveChanges();

        var album = new Album
        {
            Name = $"Album-{Guid.NewGuid():N}",
            ArtistId = artist.Id,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(album);
        db.SaveChanges();

        var song = new Song
        {
            Title = $"Song-{Guid.NewGuid():N}",
            Label = "Label",
            AlbumId = album.Id,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            RepositoryPath = "/music/song.mp3",
            Checksum = checksum,
            ChecksumAlgorithm = checksumAlgorithm,
            Size = 1000,
            Duration = TimeSpan.FromSeconds(180),
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = modifiedAt,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Add(song);
        db.SaveChanges();
        return song;
    }

    private SongDevice CreateSongDevice(MusicDbContext db, Device device, Song song, string devicePath,
        DateTime? lastSyncedModifiedAt = null, SongSyncAction? syncAction = null)
    {
        var sd = new SongDevice
        {
            DeviceId = device.Id,
            Device = device,
            SongId = song.Id,
            Song = song,
            DevicePath = devicePath,
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = lastSyncedModifiedAt,
            SyncAction = syncAction,
        };
        db.Add(sd);
        db.SaveChanges();
        return sd;
    }

    [Fact]
    public async Task CheckSync_ServerNewerThanLastSynced_ClientUnchanged_SetsDownloadAction()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var clientModified = lastSynced;
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task CheckSync_ServerAndClientUnchanged_SkipsFile()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddMinutes(-10);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var clientModified = lastSynced.AddMinutes(-5);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task CheckSync_ServerNewer_AlreadyDownloadAction_RemainsDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: SongSyncAction.Download);

        var clientModified = lastSynced;
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task CheckSync_ServerNewer_WithinTickPrecision_NoSyncAction()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        // Ensure lastSynced ends with tick digit 0 so that adding 9 ticks stays in same bucket
        // EF Core truncates last tick digit, so IsNewerThan compares ticks/10
        var lastSynced = new DateTime((DateTime.UtcNow.AddHours(-1).Ticks / 10) * 10);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, lastSynced);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        // clientModified is only 9 ticks newer - within the same tick bucket (ticks/10 rounds down)
        var clientModified = lastSynced.AddTicks(9);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task CheckSync_ServerNewer_SyncActionUpload_NotOverridden()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: SongSyncAction.Upload);

        var clientModified = lastSynced;
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Upload);
    }

    [Fact]
    public async Task CheckSync_ServerNewerBeyondTolerance_SetsDownloadAction()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddSeconds(10);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var clientModified = lastSynced.AddSeconds(-5);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task CheckSync_ServerNewer_SyncActionRemove_NotOverridden()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: SongSyncAction.Remove);

        var clientModified = lastSynced;
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        // Act
        var response = await controller.CheckSync(device.Id, request, CancellationToken.None);

        // Assert
        response.ToCreate.ShouldBeEmpty();
        response.ToUpdate.ShouldBeEmpty();
        response.PotentialConflicts.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Remove);
    }
}
