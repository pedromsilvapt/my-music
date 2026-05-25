using System.Text.Json;
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

public class DevicesControllerCommitSyncSpecs
{
    private readonly ISyncCommitService _syncCommitService = Substitute.For<ISyncCommitService>();

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
            scenario.FileSystem,
            Substitute.For<ISyncActionsServerFactory>(),
            _syncCommitService
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

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, SyncSessionStatus status, bool isDryRun = false, string? repositoryPath = null)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = status,
            IsDryRun = isDryRun,
            RepositoryPath = repositoryPath,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private DeviceSyncSessionRecord AddRecord(MusicDbContext db, long sessionId, string filePath, SyncRecordAction action)
    {
        var record = new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = filePath,
            Action = action,
            Data = JsonSerializer.SerializeToElement(new { }),
            ProcessedAt = DateTime.UtcNow,
        };
        db.DeviceSyncSessionRecords.Add(record);
        db.SaveChanges();
        return record;
    }

    [Fact]
    public async Task CommitSync_InProgressSession_SetsStatusToCommitted()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        var updated = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.ShouldBe(SyncSessionStatus.Committed);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitSync_AlreadyCommitted_ReturnsExistingResult()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var existingCommittedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed);
        session.CompletedAt = existingCommittedAt;
        scenario.DbContext.SaveChanges();
        AddRecord(scenario.DbContext, session.Id, "/music/song.mp3", SyncRecordAction.Skipped);

        var response = await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        response.Value.CommittedAt.ShouldBe(existingCommittedAt);
        response.Value.SkippedCount.ShouldBe(1);
        await _syncCommitService.DidNotReceive().CommitAsync(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitSync_CompletedSession_ThrowsException()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed);

        await Should.ThrowAsync<Exception>(() =>
            controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitSync_CancelledSession_ThrowsException()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Cancelled);

        await Should.ThrowAsync<Exception>(() =>
            controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitSync_SessionNotFound_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var result = await controller.CommitSync(device.Id, 9999, new SyncCommitRequest(), CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CommitSync_OtherUsersSession_ReturnsNotFound()
    {
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
        var session = CreateSession(scenario.DbContext, otherDevice, SyncSessionStatus.InProgress);

        var result = await controller.CommitSync(otherDevice.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CommitSync_DefaultDirectionIsBoth()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        await controller.CommitSync(device.Id, session.Id, null, CancellationToken.None);

        await _syncCommitService.Received(1).CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitSync_CustomDirectionPassed()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "up", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest { Direction = "up" }, CancellationToken.None);

        await _syncCommitService.Received(1).CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "up", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitSync_ReturnsCorrectActionCounts()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);

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
        var response = await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        response.Value.CreateRemoteCount.ShouldBe(2);
        response.Value.SkippedCount.ShouldBe(3);
        response.Value.ErrorCount.ShouldBe(1);
        response.Value.UpdateRemoteCount.ShouldBe(0);
        response.Value.CommittedAt.ShouldBeGreaterThanOrEqualTo(beforeCommit);
    }

    [Fact]
    public async Task CommitSync_CleansUpStagingDirectory()
    {
        var scenario = new Scenario();
        var mockFs = (System.IO.Abstractions.TestingHelpers.MockFileSystem)scenario.FileSystem;
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var repoPath = "/data";
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/test.mp3", new System.IO.Abstractions.TestingHelpers.MockFileData("data"));

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task CommitSync_NoRepositoryPath_SkipsStagingCleanup()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repositoryPath: null);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, false, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        var response = await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        response.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitSync_DryRunSession_PassesIsDryRunToService()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, isDryRun: true);

        _syncCommitService.CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, true, "both", Arg.Any<CancellationToken>())
            .Returns(new SyncCommitResult { ActionCounts = new Dictionary<SyncRecordAction, int>(), CommittedAt = DateTime.UtcNow });

        await controller.CommitSync(device.Id, session.Id, new SyncCommitRequest(), CancellationToken.None);

        await _syncCommitService.Received(1).CommitAsync(Arg.Any<MusicDbContext>(), session.Id, device.Id, true, "both", Arg.Any<CancellationToken>());
    }
}