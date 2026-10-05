using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and edits an album, either from its row's context menu or from its detail page,
/// waiting for the page the editor was opened from to show the album's new name without a reload.
/// </summary>
public class EditAlbumFlow(string albumName, EditAlbumOptions changes, bool fromDetailsPage = false) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();
        var newName = changes.Name ?? albumName;

        if (fromDetailsPage)
        {
            var details = await albumsPage.GoToAlbumDetailsAsync(albumName);
            var modal = await details.OpenEditAsync();
            await modal.FillAsync(changes.Name, changes.Year);
            await modal.SaveAsync();

            await Assertions.Expect(details.Name).ToHaveTextAsync(newName);
            await details.WaitForLoadedAsync();
        }
        else
        {
            var modal = await albumsPage.OpenEditAlbumAsync(albumName);
            await modal.FillAsync(changes.Name, changes.Year);
            await modal.SaveAsync();

            await Assertions.Expect(albumsPage.Collection.GetCellsByExactText("name", newName).First).ToBeVisibleAsync();
            await albumsPage.Collection.WaitForLoadedAsync();
        }
    }
}
