using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Devices;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerSessionDeletionSpecs
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

    private DeviceSyncSession CreateSession(
        MusicDbContext db, Device device, SyncSessionStatus status, DateTime startedAt)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = startedAt,
            Status = status,
            IsDryRun = false,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private DeviceSyncSessionRecord CreateRecord(
        MusicDbContext db, DeviceSyncSession session, string filePath)
    {
        var record = new DeviceSyncSessionRecord
        {
            SessionId = session.Id,
            Session = session,
            FilePath = filePath,
            Action = SyncRecordAction.Created,
            Source = SyncRecordSource.Device,
            ProcessedAt = DateTime.UtcNow
        };
        db.DeviceSyncSessionRecords.Add(record);
        db.SaveChanges();
        return record;
    }

    #region DeleteSession

    [Fact]
    public async Task DeleteSession_CompletedSession_DeletesSessionAndRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-2));
        var record1 = CreateRecord(scenario.DbContext, session, "/music/song1.mp3");
        var record2 = CreateRecord(scenario.DbContext, session, "/music/song2.mp3");

        // Act
        var result = await controller.DeleteSession(device.Id, session.Id);

        // Assert
        result.Success.ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSession_SessionNotFound_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.DeleteSession(device.Id, 9999));
    }

    [Fact]
    public async Task DeleteSession_InProgressSessionRecent_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, DateTime.UtcNow);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.DeleteSession(device.Id, session.Id));
    }

    [Fact]
    public async Task DeleteSession_InProgressSessionOld_DeletesSession()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, DateTime.UtcNow.AddSeconds(-30));

        // Act
        var result = await controller.DeleteSession(device.Id, session.Id);

        // Assert
        result.Success.ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSession_OtherDeviceSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var ownDevice = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "MyPhone");
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = CreateDevice(scenario.DbContext, otherUser.Id, "OtherPhone");
        var session = CreateSession(scenario.DbContext, otherDevice, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-2));

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.DeleteSession(ownDevice.Id, session.Id));
    }

    [Fact]
    public async Task DeleteSession_OnlyDeletesTargetSessionRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var session1 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-3));
        var session2 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-2));
        CreateRecord(scenario.DbContext, session1, "/music/old.mp3");
        CreateRecord(scenario.DbContext, session2, "/music/keep.mp3");

        // Act
        await controller.DeleteSession(device.Id, session1.Id);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session2.Id).ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session2.Id).ShouldBeTrue();
    }

    #endregion

    #region PruneSessions

    [Fact]
    public async Task PruneSessions_All_DeletesAllCompletedSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var s1 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-10));
        var s2 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-5));
        CreateRecord(scenario.DbContext, s1, "/a.mp3");
        CreateRecord(scenario.DbContext, s2, "/b.mp3");

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        result.DeletedCount.ShouldBe(2);
        scenario.DbContext.DeviceSyncSessions.Count().ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count().ShouldBe(0);
    }

    [Fact]
    public async Task PruneSessions_All_ProtectsRecentInProgressSession()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var completed = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-5));
        var inProgress = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, DateTime.UtcNow);

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        result.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == completed.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == inProgress.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task PruneSessions_Default_KeepsRecentSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");

        var oldSession = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-5));
        CreateRecord(scenario.DbContext, oldSession, "/old.mp3");

        var recentSessions = Enumerable.Range(0, 10)
            .Select(i => CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = false });

        // Assert
        result.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == oldSession.Id).ShouldBeFalse();
        foreach (var rs in recentSessions)
        {
            scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == rs.Id).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task PruneSessions_Default_DeletesRecordsForDeletedSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");

        var oldSession = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-5));
        CreateRecord(scenario.DbContext, oldSession, "/old.mp3");
        CreateRecord(scenario.DbContext, oldSession, "/old2.mp3");

        var recentSession = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddMinutes(-5));
        CreateRecord(scenario.DbContext, recentSession, "/recent.mp3");

        // Act
        await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = false });

        // Assert
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == oldSession.Id).ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == recentSession.Id).ShouldBe(1);
    }

    [Fact]
    public async Task PruneSessions_DeviceNotFound_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.PruneSessions(9999, new PruneSessionsRequest { All = true }));
    }

    [Fact]
    public async Task PruneSessions_All_OldInProgressSessionCanBeDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var oldInProgress = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, DateTime.UtcNow.AddSeconds(-30));

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        result.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == oldInProgress.Id).ShouldBeFalse();
    }

    #endregion

    #region Delete Device

    [Fact]
    public async Task Delete_DeletesDeviceAndAllAssociatedData()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-2));
        CreateRecord(scenario.DbContext, session, "/music/song.mp3");

        // Act
        await controller.Delete(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.Devices.Any(d => d.Id == device.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.DeviceId == device.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_DeviceNotFound_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.Delete(9999, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_OtherUsersDevice_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = CreateDevice(scenario.DbContext, otherUser.Id, "OtherPhone");
        var controller = CreateController(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.Delete(otherDevice.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_OnlyDeletesTargetDeviceData()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device1 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone1");
        var device2 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone2");
        var session1 = CreateSession(scenario.DbContext, device1, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-2));
        var session2 = CreateSession(scenario.DbContext, device2, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-2));
        CreateRecord(scenario.DbContext, session1, "/a.mp3");
        CreateRecord(scenario.DbContext, session2, "/b.mp3");

        // Act
        await controller.Delete(device1.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.Devices.Any(d => d.Id == device1.Id).ShouldBeFalse();
        scenario.DbContext.Devices.Any(d => d.Id == device2.Id).ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session2.Id).ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session2.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_DeletesMultipleSessionsWithRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var s1 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-10));
        var s2 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed, DateTime.UtcNow.AddDays(-5));
        var s3 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Cancelled, DateTime.UtcNow.AddDays(-1));
        CreateRecord(scenario.DbContext, s1, "/a.mp3");
        CreateRecord(scenario.DbContext, s2, "/b.mp3");
        CreateRecord(scenario.DbContext, s2, "/c.mp3");
        CreateRecord(scenario.DbContext, s3, "/d.mp3");

        // Act
        await controller.Delete(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Count().ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count().ShouldBe(0);
    }

    #endregion
}
