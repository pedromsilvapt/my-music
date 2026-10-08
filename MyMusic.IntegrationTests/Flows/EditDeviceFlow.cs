using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the details page of a device through the devices list and edits the device there, waiting for the page
/// to show the device's new name without a reload. Returns the details page.
/// </summary>
public class EditDeviceFlow(string deviceName, EditDeviceOptions changes) : IFlow<DeviceDetailsPage>
{
    public async Task<DeviceDetailsPage> ExecuteAsync(IPage page)
    {
        var details = await new OpenDeviceDetailsFlow(deviceName).ExecuteAsync(page);

        var modal = await details.OpenEditAsync();
        await modal.FillAsync(changes);
        await modal.SaveAsync();

        await Assertions.Expect(details.Name).ToHaveTextAsync(changes.Name ?? deviceName);
        await details.WaitForLoadedAsync();

        return details;
    }
}
