using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AlbumDetailsPage(IPage page) : BasePage(page, "album-detail")
{
    public SongsCollectionComponent Songs => new(Root.GetByTestId("collection"));

    public ILocator SongsCount => Root.GetByTestId("album-songs-count");

    public ILocator Artist => Root.GetByTestId("album-artist");

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }
}