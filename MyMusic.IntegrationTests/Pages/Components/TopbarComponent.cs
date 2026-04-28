using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class TopbarComponent(ILocator root)
{
    public ILocator MobileBurger => root.GetByTestId("topbar-mobile-burger");
    public ILocator DesktopBurger => root.GetByTestId("topbar-desktop-burger");
    public ILocator Title => root.GetByTestId("topbar-title");
    public ILocator Username => root.GetByTestId("topbar-username");
    public ILocator Avatar => root.GetByTestId("topbar-avatar");
    public ILocator ThemeToggle => root.GetByTestId("topbar-theme-toggle");
    public ILocator PurchasesIndicator => root.GetByTestId("topbar-purchases-indicator");

    public async Task WaitForUsernameAsync(string username, int timeout = 10000)
    {
        await Username.WaitForAsync(new() { Timeout = timeout });
    }
}
