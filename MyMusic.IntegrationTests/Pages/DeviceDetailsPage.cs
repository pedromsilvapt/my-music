using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class DeviceDetailsPage(IPage page) : BasePage(page, "device-detail")
{
    public ILocator Name => Root.GetByTestId("device-name");

    /// <summary>
    /// The icon of the device: its name is in <c>data-icon</c> and its color in <c>data-color</c>.
    /// </summary>
    public ILocator Icon => Root.GetByTestId("device-icon");

    /// <summary>
    /// The naming template in use; <c>data-default</c> says whether it is the server's default one.
    /// </summary>
    public ILocator NamingTemplate => Root.GetByTestId("device-naming-template");

    /// <summary>
    /// Whether purchases are imported to the device, in <c>data-enabled</c>.
    /// </summary>
    public ILocator ImportOnPurchase => Root.GetByTestId("device-import-on-purchase");

    public ILocator SongCount => Root.GetByTestId("device-song-count");

    public ILocator LastSync => Root.GetByTestId("device-last-sync");

    public DeviceBreadcrumbsComponent Breadcrumbs => new(Root.GetByTestId("device-breadcrumbs"));

    /// <summary>
    /// The sync sessions of the device.
    /// </summary>
    public CollectionComponent Sessions => new(Root.GetByTestId("collection"));

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
        await Sessions.WaitForLoadedAsync();
    }

    /// <summary>
    /// Opens the editor of the device.
    /// </summary>
    public async Task<DeviceEditorModalComponent> OpenEditAsync()
    {
        await Root.GetByTestId("device-edit").ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var modal = new DeviceEditorModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Device" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }
}
