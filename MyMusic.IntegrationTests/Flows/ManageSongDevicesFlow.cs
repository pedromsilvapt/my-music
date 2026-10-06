using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

public class ManageSongDevicesFlow : IFlow
{
    private readonly string _songTitle;
    private readonly string _deviceName;
    private readonly string? _action;
    private readonly string? _path;

    /// <param name="action">"Add" or "Remove", or null to leave the song where it is (to only type its path).</param>
    /// <param name="path">The path to type for the song on the device, or null to keep the one shown.</param>
    public ManageSongDevicesFlow(string songTitle, string deviceName, string? action, string? path = null)
    {
        _songTitle = songTitle;
        _deviceName = deviceName;
        _action = action;
        _path = path;
    }

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(_songTitle).ExecuteAsync(page);

        var manageDevicesButton = page.GetByRole(AriaRole.Button, new() { Name = "Manage Devices" });
        await manageDevicesButton.ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();

        var manageDevicesDialog = new ManageDevicesDialogComponent(dialog);
        if (_action != null)
        {
            await manageDevicesDialog.SelectDeviceAsync(_deviceName, _action);
        }

        if (_path != null)
        {
            await manageDevicesDialog.SetSongPathAsync(_deviceName, _songTitle, _path);
        }

        await manageDevicesDialog.ApplyAsync();
    }
}
