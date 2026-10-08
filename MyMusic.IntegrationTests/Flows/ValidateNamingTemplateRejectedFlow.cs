using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the editor of a device from its details page, types a naming template with syntax errors, and asserts
/// that the editor reports them and does not let the device be saved. The editor is closed without saving.
/// </summary>
public class ValidateNamingTemplateRejectedFlow(string deviceName, string namingTemplate) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var details = await new OpenDeviceDetailsFlow(deviceName).ExecuteAsync(page);

        var modal = await details.OpenEditAsync();
        await modal.SetNamingTemplateAsync(namingTemplate);

        // The errors should be listed below the editor, and marked inside it
        await Assertions.Expect(modal.NamingPreview.Errors.First).ToBeVisibleAsync();
        await modal.NamingTemplateEditor.WaitForErrorCountAsync(await modal.NamingPreview.Errors.CountAsync());

        // No file names are previewed for a template that cannot be used
        await Assertions.Expect(modal.NamingPreview.Summary).ToHaveCountAsync(0);
        await Assertions.Expect(modal.SubmitButton).ToBeDisabledAsync();

        await modal.CancelAsync();
    }
}
