using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerSyncCheckSpecs
{
    private DevicesController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var config = Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>();
        config.Value.Returns(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = "{{ album.artist.name ?? artists[0].name ?? \"Unknown\" }}/{{ album.name ?? \"No Album\" }}/{{ simple_label }}{{ extension ?? \".mp3\" }}"
        });

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<IMusicService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            config,
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ISyncCommitService>()
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

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = false,
            Records = [],
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
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
            Album = album,
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
            Artists = [new SongArtist { Artist = artist, ArtistId = artist.Id }],
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
    public async Task CheckSync_ServerNewerThanLastSynced_ClientUnchanged_ReturnsUpdateLocalRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

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

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var updateLocalRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.UpdateLocal).ToList();
        updateLocalRecords.Count.ShouldBe(1);
        updateLocalRecords[0].SongId.ShouldBe(song.Id);
        updateLocalRecords[0].FilePath.ShouldBe("/music/song.mp3");

        response.Value.Counts.UpdateLocalCount.ShouldBe(0, "UpdateLocal is tentative and should not be counted in CheckSync response");

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateLocal)
            .ToListAsync();
        dbRecords.ShouldBeEmpty("UpdateLocal records should not be persisted to DB during CheckSync (tentative)");
    }

    [Fact]
    public async Task CheckSync_ServerAndClientUnchanged_SkipsFile()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

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

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var nonSkippedRecords = response.Value.Records.Where(r => r.Action != SyncRecordAction.Skipped).ToList();
        var skippedRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.Skipped).ToList();
        nonSkippedRecords.ShouldBeEmpty();
        skippedRecords.Count.ShouldBe(1);

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action != SyncRecordAction.Skipped)
            .ToListAsync();
        dbRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckSync_ServerNewer_WithinTickPrecision_NoSessionRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = new DateTime((DateTime.UtcNow.AddHours(-1).Ticks / 10) * 10);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, lastSynced);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var clientModified = lastSynced.AddTicks(9);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var nonSkippedRecords = response.Value.Records.Where(r => r.Action != SyncRecordAction.Skipped).ToList();
        nonSkippedRecords.ShouldBeEmpty();

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action != SyncRecordAction.Skipped)
            .ToListAsync();
        dbRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckSync_ServerNewer_AlreadyDownloadAction_ReturnsUpdateLocalRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

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

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var updateLocalRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.UpdateLocal).ToList();
        updateLocalRecords.Count.ShouldBe(1);
        updateLocalRecords[0].SongId.ShouldBe(song.Id);

        response.Value.Counts.UpdateLocalCount.ShouldBe(0, "UpdateLocal is tentative and should not be counted in CheckSync response");

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Download);

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateLocal)
            .ToListAsync();
        dbRecords.ShouldBeEmpty("UpdateLocal records should not be persisted to DB during CheckSync (tentative)");
    }

    [Fact]
    public async Task CheckSync_ServerNewerBeyondTolerance_ReturnsUpdateLocalRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

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

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var updateLocalRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.UpdateLocal).ToList();
        updateLocalRecords.Count.ShouldBe(1);
        updateLocalRecords[0].SongId.ShouldBe(song.Id);
        updateLocalRecords[0].FilePath.ShouldBe("/music/song.mp3");

        response.Value.Counts.UpdateLocalCount.ShouldBe(0, "UpdateLocal is tentative and should not be counted in CheckSync response");

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateLocal)
            .ToListAsync();
        dbRecords.ShouldBeEmpty("UpdateLocal records should not be persisted to DB during CheckSync (tentative)");
    }

    [Fact]
    public async Task CheckSync_UnchangedFile_SyncActionRemove_CreatesUnlinkRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddMinutes(-10);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: SongSyncAction.Remove);

        var clientModified = lastSynced.AddMinutes(-5);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var unlinkRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.Count.ShouldBe(1);
        unlinkRecords[0].SongId.ShouldBe(song.Id);

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Remove);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action != SyncRecordAction.Skipped)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.Unlink);
    }

    [Fact]
    public async Task CheckSync_ServerNewer_SyncActionRemove_CreatesUnlinkRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

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

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var unlinkRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.Count.ShouldBe(1);

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBe(SongSyncAction.Remove);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action != SyncRecordAction.Skipped)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.Unlink);
    }

    [Fact]
    public async Task CheckSync_ServerNewer_NoActiveSession_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
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

        var response = await controller.CheckSync(device.Id, 0, request, CancellationToken.None);

        response.Result.ShouldNotBeNull();
        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CheckSync_ClientNewerThanServer_NoSyncActionMutation()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-3);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var clientModified = DateTime.UtcNow.AddHours(-1);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        updatedSd.SyncAction.ShouldBeNull();
        updatedSd.LastSyncedModifiedAt.ShouldBe(lastSynced);
    }

    [Fact]
    public async Task CheckSync_OnlyCreatesSessionRecords_NoSyncActionMutations()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);
        var sdId = sd.Id;

        var clientModified = lastSynced;
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var unchangedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sdId);
        unchangedSd.SyncAction.ShouldBeNull();
        unchangedSd.LastSyncedModifiedAt.ShouldBe(lastSynced);

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateLocal)
            .ToListAsync();
        dbRecords.ShouldBeEmpty("UpdateLocal records should not be persisted to DB during CheckSync (tentative)");
    }

    [Fact]
    public async Task CheckSync_ForceFlag_ReturnsUpdateRemoteRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddMinutes(-30);
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
            Force = true,
        };

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var updateRemoteRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.UpdateRemote).ToList();
        updateRemoteRecords.ShouldNotBeEmpty();

        var unchangedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        unchangedSd.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task CheckSync_BothServerAndClientNewer_ReturnsConflictRecord_NotPersistedToDb()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device);
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);

        var lastSynced = DateTime.UtcNow.AddHours(-3);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id, serverModified);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var clientModified = DateTime.UtcNow.AddHours(-2);
        var request = new SyncCheckRequest
        {
            Files =
            [
                new SyncFileInfoItem { Path = "/music/song.mp3", ModifiedAt = clientModified, CreatedAt = DateTime.UtcNow }
            ],
            Force = false,
        };

        var response = await controller.CheckSync(device.Id, session.Id, request, CancellationToken.None);

        var conflictRecords = response.Value.Records.Where(r => r.Action == SyncRecordAction.Conflict).ToList();
        conflictRecords.Count.ShouldBe(1);
        conflictRecords[0].SongId.ShouldBe(song.Id);

        response.Value.Counts.ConflictCount.ShouldBe(0, "Conflict is tentative and should not be counted in CheckSync response");

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.Conflict)
            .ToListAsync();
        dbRecords.ShouldBeEmpty("Conflict records should not be persisted to DB during CheckSync (tentative)");
    }
}