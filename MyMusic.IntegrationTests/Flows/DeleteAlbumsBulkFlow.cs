using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page, selects several albums and deletes them all at once through the selection's
/// Actions menu, confirming the deletion. Returns the warning the confirmation dialog showed about the songs that
/// still reference the albums, or <see langword="null"/> when it showed none.
/// </summary>
public class DeleteAlbumsBulkFlow(params string[] albumNames) : IFlow<string?>
{
    public async Task<string?> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var dialog = await albumsPage.OpenDeleteAlbumsAsync(albumNames);
        var warning = await dialog.GetWarningAsync();
        await dialog.ConfirmAsync();

        // The dialog closes once the delete succeeds, but the list refetch may still be in flight.
        // Wait for the deleted rows to actually disappear.
        foreach (var name in albumNames)
            await Assertions.Expect(albumsPage.Collection.GetCellsByExactText("name", name)).ToHaveCountAsync(0);
        await albumsPage.Collection.WaitForLoadedAsync();

        return warning;
    }
}
