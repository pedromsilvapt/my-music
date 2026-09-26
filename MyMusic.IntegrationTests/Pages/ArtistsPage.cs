using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class ArtistsPage(IPage page) : BasePage(page, "artists")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    /// <summary>
    /// Clicks the artist's name link and waits for its detail page to load.
    /// </summary>
    public async Task<ArtistDetailsPage> GoToArtistDetailsAsync(string artistName)
    {
        await Collection.GoToDetailsByCellTextAsync("name", artistName);
        var details = new ArtistDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }
}
