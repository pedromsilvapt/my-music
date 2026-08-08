using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class SettingsPage(IPage page) : BasePage(page, "settings")
{
    public LanguageSelectComponent LanguageSelect => new(Root.GetByTestId("settings-language"));

    /// <summary>
    /// Waits for the settings page to finish loading (data-loading="false").
    /// </summary>
    public async Task WaitForLoadedAsync(int timeout = 10000)
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = timeout });
    }
}