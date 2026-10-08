using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the editor of a device from its details page, types a naming template and asserts the preview it shows:
/// how many files the device has, and which ones the template would rename, as pairs of their current and
/// their new path. The editor is closed without saving.
/// </summary>
public class ValidateNamingPreviewFlow(
    string deviceName,
    string namingTemplate,
    int total,
    params (string CurrentPath, string NewPath)[] renames) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var details = await new OpenDeviceDetailsFlow(deviceName).ExecuteAsync(page);

        var modal = await details.OpenEditAsync();
        await modal.SetNamingTemplateAsync(namingTemplate);

        await Assertions.Expect(modal.NamingPreview.Summary).ToHaveAttributeAsync("data-total", total.ToString());
        await Assertions.Expect(modal.NamingPreview.Summary).ToHaveAttributeAsync("data-renamed", renames.Length.ToString());

        var previewed = await modal.NamingPreview.GetRenamesAsync();
        previewed.OrderBy(r => r.CurrentPath).ShouldBe(renames.OrderBy(r => r.CurrentPath));

        await modal.CancelAsync();
    }
}
