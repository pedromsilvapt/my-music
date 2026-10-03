using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the songs page and types into the collection's search box, optionally making the search
/// also match lyrics.
/// </summary>
public class SearchSongsFlow(string text, bool searchLyrics = false) : IFlow<SongsCollectionComponent>
{
    public async Task<SongsCollectionComponent> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var collection = songsPage.Collection;

        await collection.SetLyricsSearchAsync(searchLyrics);
        await collection.SearchAsync(text);

        return collection;
    }
}
