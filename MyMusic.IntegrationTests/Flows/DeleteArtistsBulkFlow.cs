using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page, selects several artists and deletes them all at once through the selection's
/// Actions menu, confirming the deletion. Returns the warning the confirmation dialog showed about the songs that
/// still reference the artists, or <see langword="null"/> when it showed none.
/// </summary>
public class DeleteArtistsBulkFlow(params string[] artistNames) : IFlow<string?>
{
    public async Task<string?> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        var dialog = await artistsPage.OpenDeleteArtistsAsync(artistNames);
        var warning = await dialog.GetWarningAsync();
        await dialog.ConfirmAsync();

        // The dialog closes once the delete succeeds, but the list refetch may still be in flight.
        // Wait for the deleted rows to actually disappear.
        foreach (var name in artistNames)
            await Assertions.Expect(artistsPage.Collection.GetCellsByExactText("name", name)).ToHaveCountAsync(0);
        await artistsPage.Collection.WaitForLoadedAsync();

        return warning;
    }
}
