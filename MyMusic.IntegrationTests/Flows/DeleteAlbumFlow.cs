using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and deletes an album, either from its row's context menu or from its detail
/// page, confirming the deletion. Returns the warning the confirmation dialog showed about the songs that still
/// reference the album, or <see langword="null"/> when it showed none.
/// </summary>
public class DeleteAlbumFlow(string albumName, bool fromDetailsPage = false) : IFlow<string?>
{
    public async Task<string?> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        // Open the deletion dialog from where the user chose to delete
        var dialog = fromDetailsPage
            ? await (await albumsPage.GoToAlbumDetailsAsync(albumName)).OpenDeleteAsync()
            : await albumsPage.OpenDeleteAlbumAsync(albumName);

        var warning = await dialog.GetWarningAsync();
        await dialog.ConfirmAsync();

        // Either way the user ends up on the albums list
        await page.WaitForURLAsync("**/albums");
        await albumsPage.Collection.WaitForLoadedAsync();

        return warning;
    }
}
