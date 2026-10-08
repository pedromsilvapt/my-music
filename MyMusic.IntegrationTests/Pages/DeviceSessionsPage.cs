using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class DeviceSessionsPage(IPage page) : BasePage(page, "device-sessions")
{
    public DeviceBreadcrumbsComponent Breadcrumbs => new(Root.GetByTestId("device-breadcrumbs"));

    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
        await Collection.WaitForLoadedAsync();
    }

    /// <summary>
    /// Goes to the details page of the device, through the device's name in the breadcrumbs.
    /// </summary>
    public async Task<DeviceDetailsPage> GoToDeviceDetailsAsync(string deviceName)
    {
        await Breadcrumbs.GetLink(deviceName).ClickAsync();
        var details = new DeviceDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }
}
