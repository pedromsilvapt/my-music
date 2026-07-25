using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSessionLookupServiceSpecs
{
    private readonly SyncSessionLookupService _service = new();

    [Fact]
    public async Task FindSessionAsync_SessionForOwnedDevice_ReturnsSession()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var result = await _service.FindSessionAsync(scenario.DbContext, session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(session.Id);
    }

    [Fact]
    public async Task FindSessionAsync_SessionForOtherUsersDevice_ReturnsNull()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);

        var result = await _service.FindSessionAsync(scenario.DbContext, session.Id, otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindSessionAsync_SessionForDifferentDevice_ReturnsNull()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var otherDevice = scenario.CreateDevice("Other");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var result = await _service.FindSessionAsync(scenario.DbContext, session.Id, otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindSessionAsync_UnknownSessionId_ReturnsNull()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        var result = await _service.FindSessionAsync(scenario.DbContext, 9999, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveSessionAsync_InProgressSession_ReturnsSession()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var result = await _service.GetActiveSessionAsync(scenario.DbContext, session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.Found.ShouldBeTrue();
        result.Failure.ShouldBe(ActiveSessionFailure.NotFound); // default enum, not used when Found
        result.Session.ShouldBe(session);
    }

    [Fact]
    public async Task GetActiveSessionAsync_UnknownSession_ReturnsNotFoundFailure()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        var result = await _service.GetActiveSessionAsync(scenario.DbContext, 9999, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(ActiveSessionFailure.NotFound);
        result.Session.ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveSessionAsync_OtherUsersSession_ReturnsNotFoundFailure()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);

        var result = await _service.GetActiveSessionAsync(scenario.DbContext, session.Id, otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(ActiveSessionFailure.NotFound);
    }

    [Fact]
    public async Task GetActiveSessionAsync_CommittedSession_ReturnsNotInProgressFailure()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed);

        var result = await _service.GetActiveSessionAsync(scenario.DbContext, session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(ActiveSessionFailure.NotInProgress);
        result.NotInProgressSessionId.ShouldBe(session.Id);
        result.NotInProgressStatus.ShouldBe(SyncSessionStatus.Committed);
    }

    [Fact]
    public async Task GetActiveSessionAsync_CancelledSession_ReturnsNotInProgressFailure()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled);

        var result = await _service.GetActiveSessionAsync(scenario.DbContext, session.Id, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(ActiveSessionFailure.NotInProgress);
        result.NotInProgressStatus.ShouldBe(SyncSessionStatus.Cancelled);
    }
}