using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class SongsPage(IPage page, bool shared = false) : BasePage(page, shared ? "shared-songs" : "songs")
{
    public SongsCollectionComponent Collection => new(Root.GetByTestId("collection"));
}