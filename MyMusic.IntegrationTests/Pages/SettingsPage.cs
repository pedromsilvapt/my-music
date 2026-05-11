using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages;

public class SettingsPage(IPage page) : BasePage(page, "settings");
