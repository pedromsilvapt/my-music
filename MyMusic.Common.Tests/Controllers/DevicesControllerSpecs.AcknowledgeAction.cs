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

public class DevicesControllerAcknowledgeActionSpecs
{
    private DevicesController CreateController(Scenario scenario, ISyncCommitService? syncCommitService = null, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            syncCommitService ?? Substitute.For<ISyncCommitService>(),
            Substitute.For<ISyncUploadService>()
        );
    }

    private ISyncCommitService CreateRealAcknowledgeService()
    {
        var service = Substitute.For<ISyncCommitService>();
        service.AcknowledgeRecordsAsync(Arg.Any<List<DeviceSyncSessionRecord>>(), Arg.Any<DateTime?>())
            .Returns(call =>
            {
                SyncCommitService.AcknowledgeRecords(
                    call.ArgAt<List<DeviceSyncSessionRecord>>(0),
                    call.ArgAt<DateTime?>(1));
                return Task.CompletedTask;
            });
        return service;
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

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, SyncSessionStatus status, bool isDryRun = false)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = status,
            IsDryRun = isDryRun,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private Song CreateSong(MusicDbContext db)
    {
        var ownerId = db.Users.First().Id;

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
            Checksum = "abc123",
            ChecksumAlgorithm = "SHA256",
            Size = 1000,
            Duration = TimeSpan.FromSeconds(180),
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Songs.Add(song);
        db.SaveChanges();
        return song;
    }

    private SongDevice CreateSongDevice(MusicDbContext db, Device device, Song? song, string path, SongSyncAction? syncAction = null)
    {
        var songDevice = new SongDevice
        {
            DeviceId = device.Id,
            Device = device,
            DevicePath = path,
            SongId = song?.Id,
            Song = song,
            SyncAction = syncAction,
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        return songDevice;
    }

    private DeviceSyncSessionRecord CreateRecord(MusicDbContext db, long sessionId, SyncRecordAction action, string filePath, long? songId = null, bool acknowledged = false)
    {
        var record = new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = filePath,
            Action = action,
            SongId = songId,
            Acknowledged = acknowledged,
            ProcessedAt = DateTime.UtcNow,
        };
        db.DeviceSyncSessionRecords.Add(record);
        db.SaveChanges();
        return record;
    }

    [Fact]
    public async Task AcknowledgeAction_WithValidRecordIds_SetsAcknowledgedTrue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var record = CreateRecord(scenario.DbContext, session.Id, SyncRecordAction.CreateLocal, "/music/song.mp3", song.Id);

        var response = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [record.Id] }, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();

        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAction_WithModifiedAt_UpdatesDataInRecord()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = song.Id });
        var record = CreateRecord(scenario.DbContext, session.Id, SyncRecordAction.CreateLocal, "/music/song.mp3", song.Id);
        record.Data = data;
        scenario.DbContext.SaveChanges();

        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var response = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [record.Id], ModifiedAt = modifiedAt }, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();

        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
        var updatedData = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(updated.Data);
        updatedData.ShouldNotBeNull();
        updatedData.ModifiedAt.ShouldBe(modifiedAt);
    }

    [Fact]
    public async Task AcknowledgeAction_AlreadyAcknowledged_RemainsAcknowledged()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var record = CreateRecord(scenario.DbContext, session.Id, SyncRecordAction.CreateLocal, "/music/song.mp3", song.Id, acknowledged: true);

        var response = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [record.Id] }, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();

        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAction_MultipleRecordIds_SetsAllAcknowledged()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var record1 = CreateRecord(scenario.DbContext, session.Id, SyncRecordAction.CreateLocal, "/music/song1.mp3", song.Id);
        var record2 = CreateRecord(scenario.DbContext, session.Id, SyncRecordAction.Unlink, "/music/song2.mp3", song.Id);

        var response = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [record1.Id, record2.Id] }, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();

        var updated1 = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record1.Id);
        updated1.Acknowledged.ShouldBeTrue();
        var updated2 = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record2.Id);
        updated2.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAction_WithInvalidRecordId_StillSucceeds()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

        var response = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [99999] }, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAction_WithEmptyRecordIds_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

        var result = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [] }, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AcknowledgeAction_ModifiedAtNotSetForServerActionTypes()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var record = CreateRecord(scenario.DbContext, session.Id, SyncRecordAction.CreateRemote, "/music/song.mp3", song.Id);
        var data = SyncActionDataSerializer.Serialize(new CreateRemoteData { SongId = song.Id, Checksum = "abc", Algorithm = "SHA256", ModifiedAt = DateTime.UtcNow });
        record.Data = data;
        scenario.DbContext.SaveChanges();

        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [record.Id], ModifiedAt = modifiedAt }, CancellationToken.None);

        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
        var updatedData = SyncActionDataSerializer.Deserialize<CreateRemoteData>(updated.Data);
        updatedData.ShouldNotBeNull();
        updatedData.ModifiedAt.ShouldNotBe(modifiedAt);
    }
}