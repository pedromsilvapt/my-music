using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSessionDeleteServiceSpecs
{
    private static SyncSessionDeleteService CreateService(Scenario scenario) =>
        new(
            scenario.DbContext,
            new SyncSessionLookupService(),
            scenario.FileSystem,
            Substitute.For<ILogger<SyncSessionDeleteService>>());

    [Fact]
    public async Task DeleteAsync_SessionNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(9999, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.Failure.ShouldBe(SyncSessionDeleteFailure.NotFound);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersSession_ReturnsNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        var service = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(session.Id, otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.Failure.ShouldBe(SyncSessionDeleteFailure.NotFound);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_CompletedSession_DeletesSessionAndRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session.Id, "/music/song1.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/music/song2.mp3", SyncRecordAction.CreateRemote);
        var service = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RecentInProgressSession_ReturnsInProgressFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow);
        var service = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.Failure.ShouldBe(SyncSessionDeleteFailure.InProgress);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_OldInProgressSession_DeletesSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow.AddSeconds(-30));
        var service = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OnlyDeletesTargetSessionRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-3));
        var session2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session1.Id, "/music/old.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session2.Id, "/music/keep.mp3", SyncRecordAction.CreateRemote);
        var service = CreateService(scenario);

        // Act
        await service.DeleteAsync(session1.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session2.Id).ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session2.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_CleansUpStagingDirectory()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2), repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));
        var service = CreateService(scenario);

        // Act
        await service.DeleteAsync(session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_SessionWithNullRepositoryPath_DoesNotThrow()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        var service = CreateService(scenario);

        // Act & Assert
        await Should.NotThrowAsync(async () =>
            await service.DeleteAsync(session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None));
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
    }
}