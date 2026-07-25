using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncCancelServiceSpecs
{
    private static SyncCancelService CreateService(Scenario scenario) =>
        new(
            scenario.DbContext,
            new SyncSessionLookupService(),
            scenario.FileSystem,
            Substitute.For<ILogger<SyncCancelService>>());

    [Fact]
    public async Task CancelAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.CancelAsync(device.Id, 9999, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CancelAsync_OtherUsersSession_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.CancelAsync(otherDevice.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CancelAsync_InProgressSession_SetsStatusToCancelled()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: "/data");
        var service = CreateService(scenario);

        // Act
        var result = await service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var updated = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.ShouldBe(SyncSessionStatus.Cancelled);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CancelAsync_InProgressSession_DeletesStagingDirectory()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/test.mp3", new MockFileData("data"));
        var service = CreateService(scenario);

        // Act
        var result = await service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.StagingDirectoryDeleted.ShouldBeTrue();
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task CancelAsync_NoStagingDirectory_ReportsNotDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: "/data");
        var service = CreateService(scenario);

        // Act
        var result = await service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.StagingDirectoryDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelAsync_NoRepositoryPath_ReportsNotDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: null);
        var service = CreateService(scenario);

        // Act
        var result = await service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.StagingDirectoryDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelAsync_CommittedSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelAsync_CompletedSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelledSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.CancelAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None));
    }
}