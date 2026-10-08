using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The dialog of the "Test Naming Template" tool, opened from the tools menu of the edit song modal: a naming
/// template editor that previews the path a template would give to the song. It saves nothing.
/// </summary>
public class SongNamingTemplateModalComponent(ILocator root) : BaseComponent(root)
{
    public ScribanTemplateEditorComponent NamingTemplateEditor => new(Root.GetByTestId("naming-template-editor"));

    public NamingPreviewComponent NamingPreview => new(Root.GetByTestId("naming-template-field"));

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        // The template editor is loaded on demand
        await Root.GetByTestId("naming-template-editor").Locator(".monaco-editor").First.WaitForAsync(new() { Timeout = 30000 });
        await NamingPreview.WaitForLoadedAsync();
    }

    /// <summary>
    /// Picks the naming template of a device from the starter templates, and waits for its preview.
    /// </summary>
    public async Task PickStarterAsync(string deviceName)
    {
        await Root.GetByTestId("song-naming-template-starters").ClickAsync();

        // The starter templates are rendered in a portal, outside the dialog
        await Root.Page.GetByTestId("song-naming-template-starter")
            .Filter(new() { HasText = deviceName })
            .ClickAsync();

        await WaitForNewPreviewAsync();
    }

    /// <summary>
    /// Types a naming template, and waits for its preview.
    /// </summary>
    public async Task SetNamingTemplateAsync(string namingTemplate)
    {
        await NamingTemplateEditor.SetValueAsync(namingTemplate);
        await WaitForNewPreviewAsync();
    }

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    public async Task CloseAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    private async Task WaitForNewPreviewAsync()
    {
        // The preview is asked for a moment after the template stops changing
        await Assertions.Expect(Root.GetByTestId("naming-template-field"))
            .ToHaveAttributeAsync("data-loading", "true", new() { Timeout = 5000 });
        await NamingPreview.WaitForLoadedAsync();
    }
}
