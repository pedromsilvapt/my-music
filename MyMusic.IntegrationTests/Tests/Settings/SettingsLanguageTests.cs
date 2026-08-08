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

        // The navbar settings link label should now be translated
        var settingsLabel = await home.Navbar.SettingsLink.InnerTextAsync();
        settingsLabel.ShouldBe("Definições");
    }
}