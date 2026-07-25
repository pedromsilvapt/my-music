using System.IO.Abstractions.TestingHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SyncControllerCancelSyncSpecs
{
    private SyncController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncController(
            Substitute.For<ILogger<SyncController>>(),
            currentUser,
            scenario.DbContext,
            scenario.FileSystem,
            SyncControllerHelpers.CreateSyncStartService(scenario),
            SyncControllerHelpers.CreateSyncCompleteService(scenario),
            SyncControllerHelpers.CreateSyncCancelService(scenario),
            Substitute.For<ISyncCommitService>(),
            SyncControllerHelpers.CreateSyncPendingActionsService(scenario),
            SyncControllerHelpers.CreateSyncDeviceSongsService(scenario),
            Substitute.For<ISyncCheckService>(),
            Substitute.For<ISyncResolveConflictsService>(),
            DevicesControllerHelpers.SessionLookup);
    }

    [Fact]
    public async Task CancelSync_InProgressSession_SetsStatusToCancelled()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: "/data");

        // Act
        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        // Assert
        var updated = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.ShouldBe(SyncSessionStatus.Cancelled);
        updated.CompletedAt.ShouldNotBeNull();
        response.Value.SessionId.ShouldBe(session.Id);
    }

    [Fact]
    public async Task CancelSync_InProgressSession_DeletesStagingDirectory()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var repoPath = "/data";
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/test.mp3", new MockFileData("data"));

        // Act
        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
        response.Value.StagingDirectoryDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelSync_NoStagingDirectory_ReportsNotDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: "/data");

        // Act
        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        // Assert
        response.Value.StagingDirectoryDeleted.ShouldBeFalse();
        response.Value.SessionId.ShouldBe(session.Id);
    }

    [Fact]
    public async Task CancelSync_NoRepositoryPath_ReportsNotDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: null);

        // Act
        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        // Assert
        response.Value.StagingDirectoryDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelSync_CommittedSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.CancelSync(device.Id, session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelSync_CompletedSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.CancelSync(device.Id, session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelSync_AlreadyCancelledSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.CancelSync(device.Id, session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelSync_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();

        // Act
        var result = await controller.CancelSync(device.Id, 9999, CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CancelSync_OtherUsersSession_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var controller = CreateController(scenario);
        var otherDevice = new Device
        {
            Name = "OtherDevice",
            OwnerId = otherUser.Id,
            Owner = scenario.DbContext.Users.First(u => u.Id == otherUser.Id),
            Songs = []
        };
        scenario.DbContext.Add(otherDevice);
        scenario.DbContext.SaveChanges();
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);

        // Act
        var result = await controller.CancelSync(otherDevice.Id, session.Id, CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }
}