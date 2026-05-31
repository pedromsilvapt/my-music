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


    [Fact]
    public async Task AcknowledgeAction_WithValidRecordIds_SetsAcknowledgedTrue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, songId: song.Id);

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
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = song.Id });
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, data: data, songId: song.Id);

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
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, songId: song.Id, acknowledged: true);

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
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record1 = scenario.AddRecord(session.Id, "/music/song1.mp3", SyncRecordAction.CreateLocal, songId: song.Id);
        var record2 = scenario.AddRecord(session.Id, "/music/song2.mp3", SyncRecordAction.Unlink, songId: song.Id);

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
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var response = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [99999] }, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAction_WithEmptyRecordIds_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var result = await controller.AcknowledgeAction(device.Id, session.Id,
            new AcknowledgeActionRequest { RecordIds = [] }, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AcknowledgeAction_ModifiedAtNotSetForServerActionTypes()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario, CreateRealAcknowledgeService());
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, songId: song.Id);
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