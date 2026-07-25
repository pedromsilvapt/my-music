using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSessionListServiceSpecs
{
    private static SyncSessionListService CreateService(Scenario scenario) =>
        new(scenario.DbContext, new DeviceLookupService());

    [Fact]
    public async Task ListAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var result = await CreateService(scenario).ListAsync(9999, scenario.AdminUser.Id, count: 5, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);

        // Act
        var result = await CreateService(scenario).ListAsync(otherDevice.Id, scenario.AdminUser.Id, count: 5, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsSessionsForDeviceOrderedByStartedAtDescending()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var oldest = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-3));
        var newest = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-1));
        var middle = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));

        // Act
        var result = await CreateService(scenario).ListAsync(device.Id, scenario.AdminUser.Id, count: 5, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Sessions.Select(s => s.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
    }

    [Fact]
    public async Task ListAsync_RespectsCountLimit()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        for (var i = 0; i < 7; i++)
        {
            scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-i));
        }

        // Act
        var result = await CreateService(scenario).ListAsync(device.Id, scenario.AdminUser.Id, count: 3, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Sessions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ListAsync_IncludesRecordsForCounting()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).ListAsync(device.Id, scenario.AdminUser.Id, count: 5, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var fetched = result.Sessions.Single(s => s.Id == session.Id);
        // Ensure the Records navigation is loaded (the controller counts these in the DTO mapping).
        scenario.DbContext.Entry(fetched).Collection(s => s.Records).IsLoaded.ShouldBeTrue();
        fetched.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_OnlyReturnsSessionsForSpecifiedDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var otherDevice = scenario.CreateDevice("Tablet");
        scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);

        // Act
        var result = await CreateService(scenario).ListAsync(device.Id, scenario.AdminUser.Id, count: 5, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Sessions.Count.ShouldBe(1);
        result.Sessions.All(s => s.DeviceId == device.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_NoSessions_ReturnsEmptyList()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        // Act
        var result = await CreateService(scenario).ListAsync(device.Id, scenario.AdminUser.Id, count: 5, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Sessions.ShouldBeEmpty();
    }
}