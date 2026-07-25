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

public class SyncControllerStartSyncSpecs
{
    private SyncController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncController(
            Substitute.For<ILogger<SyncController>>(),
            currentUser,
            scenario.DbContext,
            scenario.FileSystem,
            SyncControllerHelpers.CreateSyncStartService(scenario, factory),
            SyncControllerHelpers.CreateSyncCompleteService(scenario),
            SyncControllerHelpers.CreateSyncCancelService(scenario),
            Substitute.For<ISyncCommitService>(),
            DevicesControllerHelpers.SessionLookup);
    }

    [Fact]
    public async Task StartSync_DeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act
        var response = await controller.StartSync(9999, new SyncStartRequest(), CancellationToken.None);

        // Assert
        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task StartSync_OtherUsersDevice_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var controller = CreateController(scenario);

        // Act
        var response = await controller.StartSync(otherDevice.Id, new SyncStartRequest(), CancellationToken.None);

        // Assert
        response.Result.ShouldBeOfType<NotFoundResult>();
        scenario.DbContext.DeviceSyncSessions.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task StartSync_CreatesSessionAndReturnsSessionId()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var controller = CreateController(scenario);

        // Act
        var response = await controller.StartSync(device.Id,
            new SyncStartRequest { DryRun = true, RepositoryPath = "/music" }, CancellationToken.None);

        // Assert
        response.Value.ShouldNotBeNull();
        response.Value.SessionId.ShouldBeGreaterThan(0);
        var session = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == response.Value.SessionId);
        session.DeviceId.ShouldBe(device.Id);
        session.IsDryRun.ShouldBeTrue();
        session.RepositoryPath.ShouldBe("/music");
        session.Status.ShouldBe(SyncSessionStatus.InProgress);
    }

    [Fact]
    public async Task StartSync_NullRequest_CreatesNonDryRunSessionWithNullRepositoryPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var controller = CreateController(scenario);

        // Act
        var response = await controller.StartSync(device.Id, null, CancellationToken.None);

        // Assert
        response.Value.ShouldNotBeNull();
        var session = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == response.Value.SessionId);
        session.IsDryRun.ShouldBeFalse();
        session.RepositoryPath.ShouldBeNull();
    }

    [Fact]
    public async Task StartSync_WithScanErrors_RecordsErrorActionsForEachScanError()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var factory = Substitute.For<ISyncActionsServerFactory>();
        var syncActions = Substitute.For<ISyncActionsServer>();
        factory.Create(Arg.Any<MusicDbContext>(), Arg.Any<long>(), device.Id, Arg.Any<bool>()).Returns(syncActions);
        var controller = CreateController(scenario, factory);

        var request = new SyncStartRequest
        {
            ScanErrors =
            [
                new SyncScanErrorItem { FilePath = "/a.mp3", ErrorMessage = "read failed" },
                new SyncScanErrorItem { FilePath = "/b.mp3", ErrorMessage = "checksum" },
            ],
        };

        // Act
        var response = await controller.StartSync(device.Id, request, CancellationToken.None);

        // Assert
        response.Value.ShouldNotBeNull();
        await syncActions.Received(1).ActionError("/a.mp3", "read failed", reason: "Scan error: read failed", cancellationToken: CancellationToken.None);
        await syncActions.Received(1).ActionError("/b.mp3", "checksum", reason: "Scan error: checksum", cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task StartSync_EmptyScanErrors_DoesNotInvokeSyncActions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var factory = Substitute.For<ISyncActionsServerFactory>();
        var controller = CreateController(scenario, factory);

        // Act
        var response = await controller.StartSync(device.Id,
            new SyncStartRequest { ScanErrors = [] }, CancellationToken.None);

        // Assert
        response.Value.ShouldNotBeNull();
        factory.DidNotReceive().Create(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>());
    }
}