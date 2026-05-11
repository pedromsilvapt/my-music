using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class ArtistsPage(IPage page) : BasePage(page, "artists")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
