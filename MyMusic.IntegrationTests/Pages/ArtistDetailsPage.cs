using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class ArtistDetailsPage(IPage page) : BasePage(page, "artist-detail")
{
    private ILocator Collections => Root.GetByTestId("collection");

    /// <summary>
    /// The Songs collection on the artist detail page. The Albums collection (if present) renders
    /// as a grid without song title cells, so the Songs collection is the one containing song rows.
    /// Falls back to the last collection when no song rows are found (e.g. empty song list still loading).
    /// </summary>
    public SongsCollectionComponent Songs
    {
        get
        {
            var withSongRows = Collections.Filter(new()
            {
                Has = Root.Page.Locator("td[data-testid^='collection-cell-title-']"),
            });
            return new SongsCollectionComponent(withSongRows.Nth(0));
        }
    }

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }
}