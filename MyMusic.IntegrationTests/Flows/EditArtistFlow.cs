using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page and renames an artist, either from its row's context menu or from its detail
/// page, waiting for the page the editor was opened from to show the new name without a reload.
/// </summary>
public class EditArtistFlow(string artistName, string newName, bool fromDetailsPage = false) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        if (fromDetailsPage)
        {
            var details = await artistsPage.GoToArtistDetailsAsync(artistName);
            var modal = await details.OpenEditAsync();
            await modal.FillAsync(newName);
            await modal.SaveAsync();

            await Assertions.Expect(details.Name).ToHaveTextAsync(newName);
            await details.WaitForLoadedAsync();
        }
        else
        {
            var modal = await artistsPage.OpenEditArtistAsync(artistName);
            await modal.FillAsync(newName);
            await modal.SaveAsync();

            await Assertions.Expect(artistsPage.Collection.GetCellsByExactText("name", newName).First).ToBeVisibleAsync();
            await artistsPage.Collection.WaitForLoadedAsync();
        }
    }
}
