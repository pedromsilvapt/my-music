using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page and deletes an artist, either from its row's context menu or from its detail
/// page, confirming the deletion. Returns the warning the confirmation dialog showed about the songs that still
/// reference the artist, or <see langword="null"/> when it showed none.
/// </summary>
public class DeleteArtistFlow(string artistName, bool fromDetailsPage = false) : IFlow<string?>
{
    public async Task<string?> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        // Open the deletion dialog from where the user chose to delete
        var dialog = fromDetailsPage
            ? await (await artistsPage.GoToArtistDetailsAsync(artistName)).OpenDeleteAsync()
            : await artistsPage.OpenDeleteArtistAsync(artistName);

        var warning = await dialog.GetWarningAsync();
        await dialog.ConfirmAsync();

        // Either way the user ends up on the artists list
        await page.WaitForURLAsync("**/artists");
        await artistsPage.Collection.WaitForLoadedAsync();

        return warning;
    }
}
