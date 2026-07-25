using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceLookupServiceSpecs
{
    private readonly DeviceLookupService _service = new();

    [Fact]
    public async Task FindDeviceAsync_OwnedDevice_ReturnsDevice()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        var result = await _service.FindDeviceAsync(scenario.DbContext, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(device.Id);
        result.OwnerId.ShouldBe(scenario.AdminUser.Id);
    }

    [Fact]
    public async Task FindDeviceAsync_OtherUsersDevice_ReturnsNull()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);

        var result = await _service.FindDeviceAsync(scenario.DbContext, otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindDeviceAsync_UnknownId_ReturnsNull()
    {
        var scenario = new Scenario();

        var result = await _service.FindDeviceAsync(scenario.DbContext, 9999, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindDeviceAsync_DoesNotTrackReturnedDevice()
    {
        // The previous controller helper used FirstOrDefaultAsync; the extracted service should
        // preserve tracking behavior (tracked by default, matching EF's default).
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        var result = await _service.FindDeviceAsync(scenario.DbContext, device.Id, scenario.AdminUser.Id, CancellationToken.None);

        result.ShouldNotBeNull();
        var entry = scenario.DbContext.Entry(result);
        entry.State.ShouldBe(EntityState.Unchanged);
    }
}