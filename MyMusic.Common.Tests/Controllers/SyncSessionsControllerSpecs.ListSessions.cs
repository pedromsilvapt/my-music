using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SyncSessionsControllerListSessionsSpecs
{
    private SyncSessionsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncSessionsController(
            Substitute.For<ILogger<SyncSessionsController>>(),
            currentUser,
            SyncSessionsControllerHelpers.CreateSyncSessionListService(scenario));
    }

    [Fact]
    public async Task ListSessions_ReturnsSessionsOrderedByStartedAtDescending()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var oldest = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-3));
        var newest = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-1));
        var middle = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));

        // Act
        var result = await controller.ListSessions(device.Id);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Sessions.Select(s => s.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
    }

    [Fact]
    public async Task ListSessions_DeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act & Assert
        var result = await controller.ListSessions(9999);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ListSessions_OtherUsersDevice_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);

        // Act & Assert
        var result = await controller.ListSessions(otherDevice.Id);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ListSessions_RespectsCountQueryParameter()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        for (var i = 0; i < 7; i++)
        {
            scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-i));
        }

        // Act
        var result = await controller.ListSessions(device.Id, count: 3);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Sessions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ListSessions_DefaultCount_IsFive()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        for (var i = 0; i < 8; i++)
        {
            scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-i));
        }

        // Act
        var result = await controller.ListSessions(device.Id);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Sessions.Count.ShouldBe(5);
    }

    [Fact]
    public async Task ListSessions_MapsRecordCountsToDto()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/c.mp3", SyncRecordAction.Error);

        // Act
        var result = await controller.ListSessions(device.Id);

        // Assert
        result.Value.ShouldNotBeNull();
        var dto = result.Value.Sessions.Single(s => s.Id == session.Id);
        dto.CreateRemoteCount.ShouldBe(1);
        dto.SkippedCount.ShouldBe(1);
        dto.ErrorCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListSessions_NoSessions_ReturnsEmptyList()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");

        // Act
        var result = await controller.ListSessions(device.Id);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Sessions.ShouldBeEmpty();
    }
}