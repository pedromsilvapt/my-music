using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the devices page and creates a device through the "New device" dialog. Returns the details page
/// of the new device, which the dialog leads to.
/// </summary>
public class CreateDeviceFlow(EditDeviceOptions device) : IFlow<DeviceDetailsPage>
{
    public async Task<DeviceDetailsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var devicesPage = await home.Navbar.GoToDevicesAsync();

        var modal = await devicesPage.OpenCreateDeviceAsync();
        await modal.FillAsync(device);
        await modal.SaveAsync();

        var details = new DeviceDetailsPage(page);
        await details.WaitForLoadedAsync();
        return details;
    }
}
