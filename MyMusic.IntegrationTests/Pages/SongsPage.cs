using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class SongsPage(IPage page) : BasePage(page)
{
    public SongsCollectionComponent Collection => new(Page.GetByTestId("collection"));
}
