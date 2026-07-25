using System.IO.Abstractions.TestingHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
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
            currentUser,
            scenario.DbContext,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            scenario.FileSystem,
            Substitute.For<ISyncUploadService>(),
            DevicesControllerHelpers.DeviceLookup,
            DevicesControllerHelpers.SessionLookup,
            DevicesControllerHelpers.CreateDeviceListService(scenario),
            DevicesControllerHelpers.CreateDeviceGetService(scenario),
            DevicesControllerHelpers.CreateDeviceCreateService(scenario, currentUser),
            DevicesControllerHelpers.CreateDeviceUpdateService(scenario, currentUser),
            DevicesControllerHelpers.CreateDeviceDeleteService(scenario, currentUser),
            DevicesControllerHelpers.CreateDeviceFilterValuesService(scenario)
        );
    }

    #region Delete Device

    [Fact]
    public async Task Delete_DeletesDeviceAndAllAssociatedData()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote);

        // Act
        await controller.Delete(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.Devices.Any(d => d.Id == device.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.DeviceId == device.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_DeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act & Assert
        var result = await controller.Delete(9999, CancellationToken.None);
        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_OtherUsersDevice_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var controller = CreateController(scenario);

        // Act & Assert
        var result = await controller.Delete(otherDevice.Id, CancellationToken.None);
        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_OnlyDeletesTargetDeviceData()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device1 = scenario.CreateDevice("Phone1");
        var device2 = scenario.CreateDevice("Phone2");
        var session1 = scenario.CreateSession(device1, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        var session2 = scenario.CreateSession(device2, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session2.Id, "/b.mp3", SyncRecordAction.CreateRemote);

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
        var device = scenario.CreateDevice("Phone");
        var s1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-10));
        var s2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var s3 = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled, startedAt: DateTime.UtcNow.AddDays(-1));
        scenario.AddRecord(s1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s2.Id, "/b.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s2.Id, "/c.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s3.Id, "/d.mp3", SyncRecordAction.CreateRemote);

        // Act
        await controller.Delete(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Count().ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count().ShouldBe(0);
    }

    #endregion

    #region Staging Directory Cleanup

    [Fact]
    public async Task Delete_CleansUpStagingDirectoriesForDeviceSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2), repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));

        // Act
        await controller.Delete(device.Id, CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    #endregion
}