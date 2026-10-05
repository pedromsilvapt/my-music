using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page, selects several artists and renames them all in one go through the selection's
/// Actions menu: steps through the editor renaming each artist, then saves them together. The new names are keyed
/// by the artist's current name.
/// </summary>
public class EditArtistsBulkFlow(Dictionary<string, string> newNames) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        var modal = await artistsPage.OpenEditArtistsAsync(newNames.Keys.ToArray());

        // The editor shows one artist at a time, in an order of its own
        do
        {
            await modal.FillAsync(newNames[await modal.GetNameAsync()]);
        } while (await modal.TryGoToNextAsync());

        await modal.SaveAsync();

        // The dialog closes once the edit succeeds, but the list refetch may still be in flight
        foreach (var newName in newNames.Values)
            await Assertions.Expect(artistsPage.Collection.GetCellsByExactText("name", newName).First).ToBeVisibleAsync();
        await artistsPage.Collection.WaitForLoadedAsync();
    }
}
