using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the devices page and opens the details page of a device through its name in the list.
/// </summary>
public class OpenDeviceDetailsFlow(string deviceName) : IFlow<DeviceDetailsPage>
{
    public async Task<DeviceDetailsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var devicesPage = await home.Navbar.GoToDevicesAsync();

        return await devicesPage.GoToDeviceDetailsAsync(deviceName);
    }
}
