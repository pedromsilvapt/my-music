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

public class DevicesControllerCancelSyncSpecs
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
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            scenario.FileSystem,
            Substitute.For<ISyncActionsServerFactory>(),
            _syncCommitService,
            Substitute.For<ISyncUploadService>()
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

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, SyncSessionStatus status, string? repositoryPath = null)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = status,
            IsDryRun = false,
            RepositoryPath = repositoryPath,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    [Fact]
    public async Task CancelSync_InProgressSession_SetsStatusToCancelled()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repositoryPath: "/data");

        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        var updated = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.ShouldBe(SyncSessionStatus.Cancelled);
        updated.CompletedAt.ShouldNotBeNull();
        response.Value.SessionId.ShouldBe(session.Id);
    }

    [Fact]
    public async Task CancelSync_InProgressSession_DeletesStagingDirectory()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var repoPath = "/data";
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/test.mp3", new MockFileData("data"));

        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
        response.Value.StagingDirectoryDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelSync_NoStagingDirectory_ReportsNotDeleted()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repositoryPath: "/data");

        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        response.Value.StagingDirectoryDeleted.ShouldBeFalse();
        response.Value.SessionId.ShouldBe(session.Id);
    }

    [Fact]
    public async Task CancelSync_NoRepositoryPath_ReportsNotDeleted()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repositoryPath: null);

        var response = await controller.CancelSync(device.Id, session.Id, CancellationToken.None);

        response.Value.StagingDirectoryDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelSync_CommittedSession_ThrowsException()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed);

        await Should.ThrowAsync<Exception>(() =>
            controller.CancelSync(device.Id, session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelSync_CompletedSession_ThrowsException()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Completed);

        await Should.ThrowAsync<Exception>(() =>
            controller.CancelSync(device.Id, session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelSync_AlreadyCancelledSession_ThrowsException()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Cancelled);

        await Should.ThrowAsync<Exception>(() =>
            controller.CancelSync(device.Id, session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelSync_SessionNotFound_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var result = await controller.CancelSync(device.Id, 9999, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CancelSync_OtherUsersSession_ReturnsNotFound()
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

        var result = await controller.CancelSync(otherDevice.Id, session.Id, CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }
}