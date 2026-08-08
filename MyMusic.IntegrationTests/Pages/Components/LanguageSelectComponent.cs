using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// Wraps the language <see cref="Select"/> on the settings page.
/// Uses the <c>data-testid="settings-language"</c> input as its root.
/// </summary>
public class LanguageSelectComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// The currently selected language code (e.g. "en" or "pt").
    /// Mantine Select renders a hidden input (sibling of the visible input)
    /// that holds the selected value code, not the display label.
    /// </summary>
    public async Task<string> GetValueAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var hiddenInput = Root.Page.GetByTestId("settings-language-value");
        return await hiddenInput.GetAttributeAsync("value") ?? "";
    }

    /// <summary>
    /// Opens the dropdown and picks the option matching the given language code.
    /// </summary>
    public async Task SelectAsync(string languageCode)
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        // Click the visible combobox trigger to open the dropdown
        await Root.ClickAsync();
        // Mantine v8 Combobox.Option does not render data-value as a DOM attribute,
        // so locate the option by its visible text label instead
        var label = LanguageLabels.Get(languageCode);
        var option = Root.Page.GetByRole(AriaRole.Option, new() { Name = label });
        await option.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await option.ClickAsync();
    }
}

/// <summary>
/// Maps supported language codes to their display labels in the Mantine Select.
/// Must match <c>LANGUAGE_OPTIONS</c> in <c>src/locales/index.ts</c>.
/// </summary>
internal static class LanguageLabels
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["en"] = "English",
        ["pt"] = "Português",
    };

    public static string Get(string code) =>
        Labels.TryGetValue(code, out var label) ? label : code;
}