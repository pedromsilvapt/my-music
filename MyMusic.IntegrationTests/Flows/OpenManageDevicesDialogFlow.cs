using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the dialog that manages the devices of a song, from the song's details.
/// </summary>
public class OpenManageDevicesDialogFlow(string songTitle) : IFlow<ManageDevicesDialogComponent>
{
    public async Task<ManageDevicesDialogComponent> ExecuteAsync(IPage page)
    {
        await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);

        await page.GetByRole(AriaRole.Button, new() { Name = "Manage Devices" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();

        return new ManageDevicesDialogComponent(dialog);
    }
}
