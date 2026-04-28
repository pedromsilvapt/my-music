using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageDevicesDialogComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task SelectDeviceAsync(string deviceName, string action)
    {
        var deviceRow = Root.Locator("[data-testid^='device-row-']").Filter(new() { HasText = deviceName }).First;
        var actionLabel = deviceRow.Locator($"label:has-text('{action}')");
        await actionLabel.ClickAsync();
    }

    public async Task ApplyAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = DefaultTimeout });
    }

    public async Task CancelAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = DefaultTimeout });
    }
}
