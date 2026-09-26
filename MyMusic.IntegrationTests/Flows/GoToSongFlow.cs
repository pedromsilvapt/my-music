using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the songs page and jumps to a song through the collection's "Go to item" modal,
/// either clicking its option or pressing Enter on the (auto-selected) first match.
/// </summary>
public class GoToSongFlow(string songTitle, bool useKeyboard = false) : IFlow<SongsCollectionComponent>
{
    public async Task<SongsCollectionComponent> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var collection = songsPage.Collection;
        await collection.WaitForLoadedAsync();

        var goTo = await collection.OpenGoToAsync();
        await goTo.SearchAsync(songTitle);
        if (useKeyboard)
            await goTo.SubmitWithEnterAsync();
        else
            await goTo.SelectAsync(songTitle);

        return collection;
    }
}
