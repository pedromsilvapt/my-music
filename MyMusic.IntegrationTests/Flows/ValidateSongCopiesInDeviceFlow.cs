using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Validates the paths a song is at on a device: one per copy of the song there.
/// </summary>
public class ValidateSongCopiesInDeviceFlow(string songTitle, string deviceName, IReadOnlyList<string> expectedPaths) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var manageDevicesDialog = await new OpenManageDevicesDialogFlow(songTitle).ExecuteAsync(page);

        await manageDevicesDialog.ExpandDeviceAsync(deviceName);
        await manageDevicesDialog.ValidateSongCopiesAsync(deviceName, songTitle, expectedPaths);

        await manageDevicesDialog.CancelAsync();
    }
}
