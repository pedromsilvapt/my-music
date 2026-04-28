using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

public class ManageSongDevicesFlow : IFlow
{
    private readonly string _songTitle;
    private readonly string _deviceName;
    private readonly string _action;

    public ManageSongDevicesFlow(string songTitle, string deviceName, string action)
    {
        _songTitle = songTitle;
        _deviceName = deviceName;
        _action = action;
    }

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(_songTitle).ExecuteAsync(page);

        var manageDevicesButton = page.GetByRole(AriaRole.Button, new() { Name = "Manage Devices" });
        await manageDevicesButton.ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync(new() { Timeout = 5000 });

        var manageDevicesDialog = new ManageDevicesDialogComponent(dialog);
        await manageDevicesDialog.SelectDeviceAsync(_deviceName, _action);
        await manageDevicesDialog.ApplyAsync();
    }
}
