using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class SongsPage(IPage page) : BasePage(page, "songs")
{
    public SongsCollectionComponent Collection => new(Root.GetByTestId("collection"));
}
