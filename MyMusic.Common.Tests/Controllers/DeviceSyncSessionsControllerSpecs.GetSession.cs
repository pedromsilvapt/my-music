using Microsoft.AspNetCore.Mvc;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DeviceSyncSessionsControllerGetSessionSpecs
{
    private DeviceSyncSessionsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new DeviceSyncSessionsController(
            currentUser,
            DeviceSyncSessionsControllerHelpers.CreateSyncSessionListService(scenario),
            DeviceSyncSessionsControllerHelpers.CreateSyncSessionGetService(scenario),
            DeviceSyncSessionsControllerHelpers.CreateSyncSessionRecordsQueryService(scenario),
            DeviceSyncSessionsControllerHelpers.CreateSyncSessionFilterValuesService(scenario),
            DeviceSyncSessionsControllerHelpers.CreateSyncSessionDeleteService(scenario),
            DeviceSyncSessionsControllerHelpers.CreateSyncSessionPruneService(scenario));
    }

    [Fact]
    public async Task GetSession_ReturnsSessionWithRecordCounts()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Conflict);
        scenario.AddRecord(session.Id, "/c.mp3", SyncRecordAction.Conflict);

        // Act
        var result = await controller.GetSession(device.Id, session.Id);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Session.Id.ShouldBe(session.Id);
        result.Value.Session.Status.ShouldBe(SyncSessionStatus.Completed);
        result.Value.Session.CreateRemoteCount.ShouldBe(1);
        result.Value.Session.ConflictCount.ShouldBe(2);
        result.Value.Session.ErrorCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetSession_UnknownSession_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");

        // Act & Assert
        var result = await controller.GetSession(device.Id, 9999);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSession_SessionOfAnotherDevice_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var otherDevice = scenario.CreateDevice("Tablet");
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);

        // Act & Assert
        var result = await controller.GetSession(device.Id, session.Id);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSession_OtherUsersDevice_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);

        // Act & Assert
        var result = await controller.GetSession(otherDevice.Id, session.Id);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }
}
