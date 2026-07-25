using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncAcknowledgeServiceSpecs
{
    private static ISyncCommitService CreateRealAcknowledgeService()
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

    private static SyncAcknowledgeService CreateService(Scenario scenario, ISyncCommitService? commitService = null) =>
        new(
            scenario.DbContext,
            new DeviceLookupService(),
            commitService ?? CreateRealAcknowledgeService(),
            Substitute.For<ILogger<SyncAcknowledgeService>>());

    [Fact]
    public async Task AcknowledgeAsync_DeviceNotFound_ReturnsDeviceNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(9999, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [1] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeFalse();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcknowledgeAsync_OtherUsersDevice_ReturnsDeviceNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(otherDevice.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [1] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task AcknowledgeAsync_EmptyRecordIds_ReturnsBadRequest()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.BadRequest.ShouldBeTrue();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcknowledgeAsync_NullRecordIds_ReturnsBadRequest()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = null! }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.BadRequest.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAsync_WithValidRecordIds_SetsAcknowledgedTrue()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record.Id] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.BadRequest.ShouldBeFalse();
        result.Records.Count.ShouldBe(1);
        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAsync_WithModifiedAt_UpdatesDataInRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = song.Id });
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, data: data, songId: song.Id);
        var service = CreateService(scenario);

        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record.Id], ModifiedAt = modifiedAt }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
        var updatedData = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(updated.Data);
        updatedData.ShouldNotBeNull();
        updatedData.ModifiedAt.ShouldBe(modifiedAt);
    }

    [Fact]
    public async Task AcknowledgeAsync_AlreadyAcknowledged_RemainsAcknowledged()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, songId: song.Id, acknowledged: true);
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record.Id] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAsync_MultipleRecordIds_SetsAllAcknowledged()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record1 = scenario.AddRecord(session.Id, "/music/song1.mp3", SyncRecordAction.CreateLocal, songId: song.Id);
        var record2 = scenario.AddRecord(session.Id, "/music/song2.mp3", SyncRecordAction.Unlink, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record1.Id, record2.Id] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.Records.Count.ShouldBe(2);
        var updated1 = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record1.Id);
        updated1.Acknowledged.ShouldBeTrue();
        var updated2 = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record2.Id);
        updated2.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task AcknowledgeAsync_WithInvalidRecordId_StillSucceedsWithEmptyRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [99999] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.BadRequest.ShouldBeFalse();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcknowledgeAsync_RecordsFromOtherDeviceAreNotAcknowledged()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var otherDevice = scenario.CreateDevice("OtherPhone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record.Id] }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.Records.ShouldBeEmpty();
        var unchanged = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        unchanged.Acknowledged.ShouldBeFalse();
    }

    [Fact]
    public async Task AcknowledgeAsync_DelegatesToSyncCommitServiceAcknowledgeRecordsAsync()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, songId: song.Id);
        var commitService = Substitute.For<ISyncCommitService>();
        commitService.AcknowledgeRecordsAsync(Arg.Any<List<DeviceSyncSessionRecord>>(), Arg.Any<DateTime?>())
            .Returns(Task.CompletedTask);
        var service = CreateService(scenario, commitService);
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record.Id], ModifiedAt = modifiedAt }, CancellationToken.None);

        // Assert
        await commitService.Received(1).AcknowledgeRecordsAsync(
            Arg.Is<List<DeviceSyncSessionRecord>>(list => list.Count == 1 && list[0].Id == record.Id),
            modifiedAt);
    }

    [Fact]
    public async Task AcknowledgeAsync_ModifiedAtNotSetForServerActionTypes()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var record = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, songId: song.Id);
        var data = SyncActionDataSerializer.Serialize(new CreateRemoteData { SongId = song.Id, Checksum = "abc", Algorithm = "SHA256", ModifiedAt = DateTime.UtcNow });
        record.Data = data;
        scenario.DbContext.SaveChanges();
        var service = CreateService(scenario);

        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await service.AcknowledgeAsync(device.Id, scenario.AdminUser.Id,
            new SyncAcknowledgeInput { RecordIds = [record.Id], ModifiedAt = modifiedAt }, CancellationToken.None);

        // Assert
        var updated = await scenario.DbContext.DeviceSyncSessionRecords.FirstAsync(r => r.Id == record.Id);
        updated.Acknowledged.ShouldBeTrue();
        var updatedData = SyncActionDataSerializer.Deserialize<CreateRemoteData>(updated.Data);
        updatedData.ShouldNotBeNull();
        updatedData.ModifiedAt.ShouldNotBe(modifiedAt);
    }
}