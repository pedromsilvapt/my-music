using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Selects a shared song in the shared view, opens the floating actions menu, and clicks Import.
/// </summary>
public class ImportSharedSongFlow(long sharerId, string songTitle) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        // Navigate to the shared view and select the song
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSharedSongsAsync(sharerId);
        var collection = songsPage.Collection;

        var rowIndex = await collection.FindRowByTitleAsync(songTitle);
        await collection.SelectRowByIndexAsync(rowIndex);

        // Open the floating actions menu and click Import
        var menu = await collection.OpenFloatingActionsMenuAsync();
        await menu.ImportAsync();
    }
}