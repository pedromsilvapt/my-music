using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSessionPruneServiceSpecs
{
    private static SyncSessionPruneService CreateService(Scenario scenario) =>
        new(
            scenario.DbContext,
            new DeviceLookupService(),
            scenario.FileSystem,
            Substitute.For<ILogger<SyncSessionPruneService>>());

    [Fact]
    public async Task PruneAsync_DeviceNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);

        // Act
        var result = await service.PruneAsync(9999, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        result.Failure.ShouldBe(SyncSessionPruneFailure.NotFound);
        result.DeletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task PruneAsync_OtherUsersDevice_ReturnsNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var service = CreateService(scenario);

        // Act
        var result = await service.PruneAsync(otherDevice.Id, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        result.Failure.ShouldBe(SyncSessionPruneFailure.NotFound);
    }

    [Fact]
    public async Task PruneAsync_All_DeletesAllCompletedSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var s1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-10));
        var s2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        scenario.AddRecord(s1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s2.Id, "/b.mp3", SyncRecordAction.CreateRemote);
        var service = CreateService(scenario);

        // Act
        var result = await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        result.DeletedCount.ShouldBe(2);
        scenario.DbContext.DeviceSyncSessions.Count().ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count().ShouldBe(0);
    }

    [Fact]
    public async Task PruneAsync_All_ProtectsRecentInProgressSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var completed = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var inProgress = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow);
        var service = CreateService(scenario);

        // Act
        var result = await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        result.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == completed.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == inProgress.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task PruneAsync_Default_KeepsRecentSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var oldSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        scenario.AddRecord(oldSession.Id, "/old.mp3", SyncRecordAction.CreateRemote);
        var recentSessions = Enumerable.Range(0, 10)
            .Select(i => scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();
        var service = CreateService(scenario);

        // Act
        var result = await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: false, CancellationToken.None);

        // Assert
        result.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == oldSession.Id).ShouldBeFalse();
        foreach (var rs in recentSessions)
        {
            scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == rs.Id).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task PruneAsync_Default_DeletesRecordsForDeletedSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var oldSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        scenario.AddRecord(oldSession.Id, "/old.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(oldSession.Id, "/old2.mp3", SyncRecordAction.CreateRemote);
        var recentSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddMinutes(-5));
        scenario.AddRecord(recentSession.Id, "/recent.mp3", SyncRecordAction.CreateRemote);
        var service = CreateService(scenario);

        // Act
        await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: false, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == oldSession.Id).ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == recentSession.Id).ShouldBe(1);
    }

    [Fact]
    public async Task PruneAsync_All_OldInProgressSessionCanBeDeleted()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var oldInProgress = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, startedAt: DateTime.UtcNow.AddSeconds(-30));
        var service = CreateService(scenario);

        // Act
        var result = await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        result.DeletedCount.ShouldBe(1);
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == oldInProgress.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task PruneAsync_CleansUpStagingDirectoriesForDeletedSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var oldSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5), repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{oldSession.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));
        var service = CreateService(scenario);

        // Act
        await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task PruneAsync_OnlyAffectsTargetDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var otherDevice = scenario.CreateDevice("Tablet");
        var ownOld = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var otherOld = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var service = CreateService(scenario);

        // Act
        await service.PruneAsync(device.Id, scenario.AdminUser.Id, all: true, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == ownOld.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == otherOld.Id).ShouldBeTrue();
    }
}