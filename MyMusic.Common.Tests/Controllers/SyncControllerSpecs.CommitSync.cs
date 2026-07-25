using Microsoft.AspNetCore.Mvc;
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

public class SyncControllerCommitSyncSpecs
{
    private readonly ISyncCommitService _syncCommitService = Substitute.For<ISyncCommitService>();

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
            _syncCommitService,
            SyncControllerHelpers.CreateSyncPendingActionsService(scenario),
            SyncControllerHelpers.CreateSyncDeviceSongsService(scenario),
            Substitute.For<ISyncCheckService>(),
            Substitute.For<ISyncResolveConflictsService>(),
            DevicesControllerHelpers.SessionLookup);
    }

    [Fact]
    public async Task CommitSync_InProgressSession_SetsStatusToCommitted()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        // Act
        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        var updated = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.ShouldBe(SyncSessionStatus.Committed);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitSync_AlreadyCommitted_ReturnsExistingResult()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var existingCommittedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);
        session.CompletedAt = existingCommittedAt;
        scenario.DbContext.SaveChanges();
        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.Skipped);

        // Act
        var response = await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        response.Value.CommittedAt.ShouldBe(existingCommittedAt);
        response.Value.SkippedCount.ShouldBe(1);
        await _syncCommitService.DidNotReceive().CommitAsync(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitSync_CompletedSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitSync_CancelledSession_ThrowsException()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitSync_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();

        // Act
        var result = await controller.CommitSync(device.Id, 9999, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CommitSync_OtherUsersSession_ReturnsNotFound()
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
        var result = await controller.CommitSync(otherDevice.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CommitSync_DefaultDirectionIsBoth()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        // Act
        await controller.CommitSync(device.Id, session.Id, null, CancellationToken.None);

        // Assert
        await _syncCommitService.Received(1).CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitSync_CustomDirectionPassed()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "up", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        // Act
        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest { Direction = "up" }, CancellationToken.None);

        // Assert
        await _syncCommitService.Received(1).CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "up", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitSync_ReturnsCorrectActionCounts()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var committedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult
            {
                ActionCounts = new Dictionary<SyncRecordAction, int>
                {
                    [SyncRecordAction.CreateRemote] = 2,
                    [SyncRecordAction.Skipped] = 3,
                    [SyncRecordAction.Error] = 1,
                },
                CommittedAt = committedAt,
            });

        var beforeCommit = DateTime.UtcNow;

        // Act
        var response = await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        response.Value.CreateRemoteCount.ShouldBe(2);
        response.Value.SkippedCount.ShouldBe(3);
        response.Value.ErrorCount.ShouldBe(1);
        response.Value.UpdateRemoteCount.ShouldBe(0);
        response.Value.CommittedAt.ShouldBeGreaterThanOrEqualTo(beforeCommit);
    }

    [Fact]
    public async Task CommitSync_CleansUpStagingDirectory()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (System.IO.Abstractions.TestingHelpers.MockFileSystem)scenario.FileSystem;
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var repoPath = "/data";
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/test.mp3", new System.IO.Abstractions.TestingHelpers.MockFileData("data"));

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        // Act
        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task CommitSync_NoRepositoryPath_SkipsStagingCleanup()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: null);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        // Act
        var response = await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        response.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitSync_DryRunSession_PassesIsDryRunToService()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, isDryRun: true);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, true, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        // Act
        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        // Assert
        await _syncCommitService.Received(1).CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, true, "both", Arg.Any<CancellationToken>());
    }
}