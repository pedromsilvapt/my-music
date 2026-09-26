using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class PlayerPage(IPage page) : BasePage(page, "player")
{
    public SongsCollectionComponent Collection => new(Root.GetByTestId("collection"));
}
