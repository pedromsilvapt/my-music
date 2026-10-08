using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class DevicesPage(IPage page) : BasePage(page, "devices")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    /// <summary>
    /// Goes to the details page of a device, through its name in the list.
    /// </summary>
    public async Task<DeviceDetailsPage> GoToDeviceDetailsAsync(string deviceName)
    {
        await Collection.GoToDetailsByCellTextAsync("name", deviceName);
        var details = new DeviceDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }

    /// <summary>
    /// Goes to the sync sessions of a device, through its row's context menu.
    /// </summary>
    public async Task<DeviceSessionsPage> GoToDeviceSessionsAsync(string deviceName)
    {
        await Collection.ClickRowActionAsync("name", deviceName, "View Sessions");
        var sessions = new DeviceSessionsPage(Page);
        await sessions.WaitForLoadedAsync();
        return sessions;
    }

    /// <summary>
    /// Opens the "New device" dialog from the toolbar.
    /// </summary>
    public async Task<DeviceEditorModalComponent> OpenCreateDeviceAsync()
    {
        await Root.GetByTestId("create-device").ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var modal = new DeviceEditorModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "New Device" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }
}
