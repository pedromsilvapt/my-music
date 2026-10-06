using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the songs page and applies an advanced filter expression to its collection.
/// </summary>
public class FilterSongsFlow(string filter) : IFlow<SongsCollectionComponent>
{
    public async Task<SongsCollectionComponent> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var collection = songsPage.Collection;

        await collection.ApplyFilterAsync(filter);

        return collection;
    }
}
