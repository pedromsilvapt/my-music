using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AlbumsPage(IPage page) : BasePage(page, "albums")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
