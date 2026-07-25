using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncCompleteServiceSpecs
{
    private static SyncCompleteService CreateService(Scenario scenario) =>
        new(
            scenario.DbContext,
            new SyncSessionLookupService(),
            Substitute.For<ILogger<SyncCompleteService>>());

    [Fact]
    public async Task CompleteAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.CompleteAsync(device.Id, 9999, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_OtherUsersSession_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Committed);
        var service = CreateService(scenario);

        // Act
        var result = await service.CompleteAsync(otherDevice.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_UpdatesDeviceLastSyncAt()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        device.LastSyncAt.ShouldBe(null);
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);
        var beforeComplete = DateTime.UtcNow;
        var service = CreateService(scenario);

        // Act
        await service.CompleteAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldNotBeNull();
        updatedDevice.LastSyncAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public async Task CompleteAsync_DryRun_DoesNotUpdateDeviceLastSyncAt()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed, isDryRun: true);
        var service = CreateService(scenario);

        // Act
        await service.CompleteAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_RejectsInProgressSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.CompleteAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_RejectsCompletedSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.CompleteAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_SetsSessionStatusToCompleted()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);
        var beforeComplete = DateTime.UtcNow;
        var service = CreateService(scenario);

        // Act
        await service.CompleteAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        var updatedSession = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updatedSession.Status.ShouldBe(SyncSessionStatus.Completed);
        updatedSession.CompletedAt.ShouldNotBeNull();
        updatedSession.CompletedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsPerActionTypeCounts()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);

        scenario.DbContext.DeviceSyncSessionRecords.AddRange(
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.CreateRemote, FilePath = "/a", ProcessedAt = DateTime.UtcNow },
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.CreateRemote, FilePath = "/b", ProcessedAt = DateTime.UtcNow },
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.Skipped, FilePath = "/c", ProcessedAt = DateTime.UtcNow },
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.Link, FilePath = "/d", ProcessedAt = DateTime.UtcNow }
        );
        scenario.DbContext.SaveChanges();
        var service = CreateService(scenario);

        // Act
        var result = await service.CompleteAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.CreateRemoteCount.ShouldBe(2);
        result.SkippedCount.ShouldBe(1);
        result.LinkCount.ShouldBe(1);
        result.UpdateRemoteCount.ShouldBe(0);
        result.DeleteLocalCount.ShouldBe(0);
        result.UnlinkCount.ShouldBe(0);
        result.ErrorCount.ShouldBe(0);
    }
}