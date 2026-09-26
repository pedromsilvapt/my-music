using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AlbumsPage(IPage page) : BasePage(page, "albums")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    /// <summary>
    /// Clicks the album's name link and waits for its detail page to load.
    /// </summary>
    public async Task<AlbumDetailsPage> GoToAlbumDetailsAsync(string albumName)
    {
        await Collection.GoToDetailsByCellTextAsync("name", albumName);
        var details = new AlbumDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }
}
