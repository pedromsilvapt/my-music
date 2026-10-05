using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page, selects several albums and edits them all in one go through the selection's
/// Actions menu: steps through the editor changing each album, then saves them together. The changes are keyed by
/// the album's current name.
/// </summary>
public class EditAlbumsBulkFlow(Dictionary<string, EditAlbumOptions> changes) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var modal = await albumsPage.OpenEditAlbumsAsync(changes.Keys.ToArray());

        // The editor shows one album at a time, in an order of its own
        do
        {
            var albumChanges = changes[await modal.GetNameAsync()];
            await modal.FillAsync(albumChanges.Name, albumChanges.Year);
        } while (await modal.TryGoToNextAsync());

        await modal.SaveAsync();

        // The dialog closes once the edit succeeds, but the list refetch may still be in flight
        foreach (var (albumName, albumChanges) in changes)
            await Assertions.Expect(albumsPage.Collection.GetCellsByExactText("name", albumChanges.Name ?? albumName).First)
                .ToBeVisibleAsync();
        await albumsPage.Collection.WaitForLoadedAsync();
    }
}
