using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// A Scriban template code editor.
/// </summary>
public class ScribanTemplateEditorComponent(ILocator root) : BaseComponent(root)
{
    private ILocator Editor => Root.Locator(".monaco-editor").First;

    /// <summary>
    /// Replaces the template with the given text.
    /// </summary>
    public async Task SetValueAsync(string template)
    {
        await Editor.ClickAsync();
        await Root.Page.Keyboard.PressAsync("ControlOrMeta+A");

        await Root.Page.Keyboard.PressAsync("Delete");

        // Typed key by key, as a user does: the editor closes each "{{" by itself, and typing "}}" goes over it
        await Root.Page.Keyboard.TypeAsync(template);
    }

    /// <summary>
    /// Waits for the editor to mark the given number of syntax errors in the template.
    /// </summary>
    public Task WaitForErrorCountAsync(int count) => WaitForAttributeAsync("data-errors", count.ToString());
}
