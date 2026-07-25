using System.IO.Abstractions.TestingHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Devices;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SyncSessionsControllerSessionDeletionSpecs
{
    private SyncSessionsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncSessionsController(
            currentUser,
            SyncSessionsControllerHelpers.CreateSyncSessionListService(scenario),
            SyncSessionsControllerHelpers.CreateSyncSessionRecordsQueryService(scenario),
            SyncSessionsControllerHelpers.CreateSyncSessionFilterValuesService(scenario),
            SyncSessionsControllerHelpers.CreateSyncSessionDeleteService(scenario),
            SyncSessionsControllerHelpers.CreateSyncSessionPruneService(scenario));
    }

    #region DeleteSession

    [Fact]
    public async Task DeleteSession_CompletedSession_DeletesSessionAndRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session.Id, "/music/song1.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/music/song2.mp3", SyncRecordAction.CreateRemote);

        // Act
        var result = await controller.DeleteSession(device.Id, session.Id);

        // Assert
        result.Value.Success.ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSession_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");

        // Act & Assert
        var result = await controller.DeleteSession(device.Id, 9999);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteSession_InProgressSessionRecent_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow);

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
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow.AddSeconds(-30));

        // Act
        var result = await controller.DeleteSession(device.Id, session.Id);

        // Assert
        result.Value.Success.ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSession_OtherDeviceSession_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var ownDevice = scenario.CreateDevice("MyPhone");
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));

        // Act & Assert
        var result = await controller.DeleteSession(ownDevice.Id, session.Id);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteSession_OnlyDeletesTargetSessionRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-3));
        var session2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session1.Id, "/music/old.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session2.Id, "/music/keep.mp3", SyncRecordAction.CreateRemote);

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
        var device = scenario.CreateDevice("Phone");
        var s1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-10));
        var s2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        scenario.AddRecord(s1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s2.Id, "/b.mp3", SyncRecordAction.CreateRemote);

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        result.Value.DeletedCount.ShouldBe(2);
        scenario.DbContext.DeviceSyncSessions.Count().ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count().ShouldBe(0);
    }

    [Fact]
    public async Task PruneSessions_All_ProtectsRecentInProgressSession()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var completed = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var inProgress = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow);

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        result.Value.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == completed.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == inProgress.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task PruneSessions_Default_KeepsRecentSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");

        var oldSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        scenario.AddRecord(oldSession.Id, "/old.mp3", SyncRecordAction.CreateRemote);

        var recentSessions = Enumerable.Range(0, 10)
            .Select(i => scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = false });

        // Assert
        result.Value.DeletedCount.ShouldBe(1);
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
        var device = scenario.CreateDevice("Phone");

        var oldSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        scenario.AddRecord(oldSession.Id, "/old.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(oldSession.Id, "/old2.mp3", SyncRecordAction.CreateRemote);

        var recentSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddMinutes(-5));
        scenario.AddRecord(recentSession.Id, "/recent.mp3", SyncRecordAction.CreateRemote);

        // Act
        await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = false });

        // Assert
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == oldSession.Id).ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == recentSession.Id).ShouldBe(1);
    }

    [Fact]
    public async Task PruneSessions_DeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act & Assert
        var result = await controller.PruneSessions(9999, new PruneSessionsRequest { All = true });
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PruneSessions_All_OldInProgressSessionCanBeDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var oldInProgress = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow.AddSeconds(-30));

        // Act
        var result = await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        result.Value.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == oldInProgress.Id).ShouldBeFalse();
    }

    #endregion

    #region Staging Directory Cleanup

    [Fact]
    public async Task DeleteSession_CleansUpStagingDirectory()
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
        await controller.DeleteSession(device.Id, session.Id);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task PruneSessions_CleansUpStagingDirectoriesForDeletedSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var oldSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5), repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{oldSession.Id}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));

        // Act
        await controller.PruneSessions(device.Id, new PruneSessionsRequest { All = true });

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    #endregion
}