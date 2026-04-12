using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
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
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        device.LastSyncAt.ShouldBe(null);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var beforeComplete = DateTime.UtcNow;

        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "both" }, CancellationToken.None);

        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldNotBeNull();
        updatedDevice.LastSyncAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public async Task CompleteSync_DryRun_DoesNotUpdateDeviceLastSyncAt()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        device.LastSyncAt.ShouldBe(null);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, isDryRun: true);

        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "both" }, CancellationToken.None);

        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteSync_DownDirection_UpdatesDeviceLastSyncAt()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var beforeComplete = DateTime.UtcNow;

        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "down" }, CancellationToken.None);

        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldNotBeNull();
        updatedDevice.LastSyncAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public async Task CompleteSync_UpDirection_UpdatesDeviceLastSyncAt()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var beforeComplete = DateTime.UtcNow;

        var response = await controller.CompleteSync(device.Id, session.Id,
            new SyncCompleteRequest { Direction = "up" }, CancellationToken.None);

        var updatedDevice = await scenario.DbContext.Devices.FirstAsync(d => d.Id == device.Id);
        updatedDevice.LastSyncAt.ShouldNotBeNull();
        updatedDevice.LastSyncAt.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }
}