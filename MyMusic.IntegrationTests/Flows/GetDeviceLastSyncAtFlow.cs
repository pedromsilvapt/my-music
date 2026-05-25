using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

public class GetDeviceLastSyncAtFlow(string deviceName) : IFlow<string>
{
    public async Task<string> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var devicesPage = await home.Navbar.GoToDevicesAsync();
        var rowIndex = await devicesPage.Collection.FindRowByCellTextAsync("name", deviceName);
        if (rowIndex < 0)
            throw new InvalidOperationException($"Device '{deviceName}' not found on devices page");
        return await devicesPage.Collection.GetCellTextAsync(rowIndex, "lastSyncAt");
    }
}