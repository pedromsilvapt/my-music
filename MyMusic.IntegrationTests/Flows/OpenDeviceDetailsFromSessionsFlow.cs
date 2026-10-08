using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the sync sessions of a device from the devices page, and from there to the device's details page
/// through its name in the breadcrumbs.
/// </summary>
public class OpenDeviceDetailsFromSessionsFlow(string deviceName) : IFlow<DeviceDetailsPage>
{
    public async Task<DeviceDetailsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var devicesPage = await home.Navbar.GoToDevicesAsync();
        var sessionsPage = await devicesPage.GoToDeviceSessionsAsync(deviceName);

        // The sessions page should be the last of the breadcrumbs, below the device
        await Assertions.Expect(sessionsPage.Breadcrumbs.Current).ToHaveTextAsync("Sessions");

        return await sessionsPage.GoToDeviceDetailsAsync(deviceName);
    }
}
