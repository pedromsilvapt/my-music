using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the editor of a device from its details page and tries to save changes the server should reject.
/// Returns the error the editor shows while staying open, and then closes it without saving.
/// </summary>
public class EditRejectedDeviceFlow(string deviceName, EditDeviceOptions changes) : IFlow<string>
{
    public async Task<string> ExecuteAsync(IPage page)
    {
        var details = await new OpenDeviceDetailsFlow(deviceName).ExecuteAsync(page);

        var modal = await details.OpenEditAsync();
        await modal.FillAsync(changes);

        var error = await modal.SaveRejectedAsync();
        await modal.CancelAsync();

        return error;
    }
}
