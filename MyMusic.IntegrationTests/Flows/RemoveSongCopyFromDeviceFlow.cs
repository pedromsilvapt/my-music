using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Removes the copy of a song at one path of a device, leaving the song's other copies on the device.
/// </summary>
public class RemoveSongCopyFromDeviceFlow(string songTitle, string deviceName, string path) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var manageDevicesDialog = await new OpenManageDevicesDialogFlow(songTitle).ExecuteAsync(page);

        await manageDevicesDialog.RemoveSongCopyAsync(deviceName, songTitle, path);

        await manageDevicesDialog.ApplyAsync();
    }
}
