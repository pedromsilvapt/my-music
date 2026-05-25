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

public class DevicesControllerCompleteSyncSpecs
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
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            Substitute.For<ISyncActionsServerFactory>(),
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

    [Fact]
    public async Task CompleteSync_UpdatesDeviceLastSyncAt()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        device.LastSyncAt.ShouldBe(null);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed);
        var beforeComplete = DateTime.UtcNow;

        // Act
        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "both" }, CancellationToken.None);

        // Assert
        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldNotBeNull();
        updatedDevice.LastSyncAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public async Task CompleteSync_DryRun_DoesNotUpdateDeviceLastSyncAt()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        device.LastSyncAt.ShouldBe(null);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed, isDryRun: true);

        // Act
        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "both" }, CancellationToken.None);

        // Assert
        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteSync_RejectsInProgressSession()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
            await controller.CompleteSync(device.Id, session.Id,
                new SyncCompleteRequest { Direction = "both" }, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteSync_SetsSessionStatusToCompleted()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed);
        var beforeComplete = DateTime.UtcNow;

        // Act
        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "both" }, CancellationToken.None);

        // Assert
        var updatedSession = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updatedSession.Status.ShouldBe(SyncSessionStatus.Completed);
        updatedSession.CompletedAt.ShouldNotBeNull();
        updatedSession.CompletedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public async Task CompleteSync_ReturnsPerActionTypeCounts()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed);

        scenario.DbContext.DeviceSyncSessionRecords.AddRange(
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.CreateRemote, FilePath = "/a", ProcessedAt = DateTime.UtcNow },
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.CreateRemote, FilePath = "/b", ProcessedAt = DateTime.UtcNow },
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.Skipped, FilePath = "/c", ProcessedAt = DateTime.UtcNow },
            new DeviceSyncSessionRecord { SessionId = session.Id, Action = SyncRecordAction.Link, FilePath = "/d", ProcessedAt = DateTime.UtcNow }
        );
        scenario.DbContext.SaveChanges();

        // Act
        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "both" }, CancellationToken.None);

        // Assert
        response.Value.CreateRemoteCount.ShouldBe(2);
        response.Value.SkippedCount.ShouldBe(1);
        response.Value.LinkCount.ShouldBe(1);
        response.Value.UpdateRemoteCount.ShouldBe(0);
        response.Value.DeleteCount.ShouldBe(0);
        response.Value.UnlinkCount.ShouldBe(0);
        response.Value.ErrorCount.ShouldBe(0);
    }
}