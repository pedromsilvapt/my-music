using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Settings;

public class SettingsLanguageTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Fact]
    public async Task ChangeLanguage_PersistsAfterReload()
    {
        // Navigate to settings and change the language to Portuguese
        var home = new HomePage(Page);
        var settings = await home.Navbar.GoToSettingsAsync();
        await settings.WaitForLoadedAsync();
        await settings.LanguageSelect.SelectAsync("pt");

        // Wait for the mutation + query refetch to settle so the persisted
        // value is reflected in the hidden input before reloading
        await settings.LanguageSelect.WaitUntilValueAsync("pt");

        // Reload so the preference is re-read from the server
        await Page.ReloadAsync();
        await settings.WaitForLoadedAsync();

        // The Select should reflect the persisted language
        var value = await settings.LanguageSelect.GetValueAsync();
        value.ShouldBe("pt", "Language select should show 'pt' after reload");
    }

    [Fact]
    public async Task DefaultLanguage_IsEnglish()
    {
        // A brand-new user should default to English
        var home = new HomePage(Page);
        var settings = await home.Navbar.GoToSettingsAsync();
        await settings.WaitForLoadedAsync();

        var value = await settings.LanguageSelect.GetValueAsync();
        value.ShouldBe("en", "New users should default to English");
    }

    [Fact]
    public async Task ChangeLanguage_UpdatesTranslatedString()
    {
        // Change to Portuguese — the navbar "Settings" label should become "Definições"
        var home = new HomePage(Page);
        var settings = await home.Navbar.GoToSettingsAsync();
        await settings.WaitForLoadedAsync();
        await settings.LanguageSelect.SelectAsync("pt");

        // Wait for the i18n language switch to complete and the navbar label to update
        await Assertions.Expect(home.Navbar.SettingsLink).ToHaveTextAsync("Definições");
    }
}
