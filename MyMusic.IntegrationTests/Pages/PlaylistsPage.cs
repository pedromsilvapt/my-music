using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class PlaylistsPage(IPage page) : BasePage(page, "playlists")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
