using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public abstract class BasePage(IPage page)
{
    public IPage Page { get; } = page;

    public TopbarComponent Topbar => new(Page.GetByTestId("topbar"));
    public NavbarComponent Navbar => new(Page.GetByTestId("navbar"));
}
