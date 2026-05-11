using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public abstract class BasePage(IPage page, string? testId = null)
{
    public IPage Page { get; } = page;
    protected ILocator Root { get; } = testId is not null ? page.GetByTestId(testId) : page.Locator("body");

    public TopbarComponent Topbar => new(Page.GetByTestId("topbar"));
    public NavbarComponent Navbar => new(Page.GetByTestId("navbar"));
}
