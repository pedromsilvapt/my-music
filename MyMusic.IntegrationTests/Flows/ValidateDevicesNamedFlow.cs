using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the devices page and asserts how many devices have exactly the given name.
/// </summary>
public class ValidateDevicesNamedFlow(string deviceName, int count) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var devicesPage = await home.Navbar.GoToDevicesAsync();

        await Assertions.Expect(devicesPage.Collection.GetCellsByExactText("name", deviceName)).ToHaveCountAsync(count);
    }
}
