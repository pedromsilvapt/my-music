using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;

namespace MyMusic.IntegrationTests.Tests.Devices;

/// <summary>
/// Integration tests for the details page of a device.
/// </summary>
public class DeviceDetailsTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly DevicesFixture _devices = new();

    // Scenario: A device's details page shows its configuration
    //   Given a device that was never synced
    //   When the user opens the device from the devices list
    //   Then the page shows the device's name, icon and color
    //   And the default naming template, since the device has none of its own
    //   And that purchases are not imported, that it has no songs and no sync sessions
    [Fact]
    public async Task DeviceDetails_OpenedFromTheDevicesList_ShouldShowTheDeviceConfiguration()
    {
        // Setup: a device with an icon and a color, never synced
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Action & Assert: the details page, reached through the device's name in the list, should show its options
        await new ValidateDeviceDetailsFlow(device.Name, new(
            Icon: device.Icon,
            Color: device.Color,
            ImportOnPurchase: false,
            UsesDefaultNamingTemplate: true,
            SongCount: 0,
            SessionsCount: 0)).ExecuteAsync(Page);
    }

    // Scenario: The device in the breadcrumbs of its sync sessions leads to its details page
    //   Given two devices
    //   When the user opens the sync sessions of one of them from the devices list
    //   And clicks the device's name in the breadcrumbs
    //   Then the details page of that device is shown
    [Fact]
    public async Task DeviceDetails_OpenedFromTheSessionsBreadcrumbs_ShouldShowThatDevice()
    {
        // Setup: two devices, so the breadcrumbs must lead to the right one
        var devices = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[..2]);
        var device = devices[1];

        // Action: go to the device's sessions, then back to the device through the breadcrumbs
        var details = await new OpenDeviceDetailsFromSessionsFlow(device.Name).ExecuteAsync(Page);

        // Assert: the details page should be the one of the device whose sessions were open
        await new ValidateDeviceDetailsFlow(device.Name, new(Icon: device.Icon, Color: device.Color), details)
            .ExecuteAsync(Page);
    }
}
