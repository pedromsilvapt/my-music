using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page, selects <paramref name="keptAlbumName"/> and <paramref name="otherAlbumNames"/>
/// and merges them through the selection's Actions menu, keeping the former. Returns the sentences the dialog
/// showed about what the merge changes, right before it was confirmed. The other albums must not share their name
/// with the kept one.
/// </summary>
public class MergeAlbumsFlow(string keptAlbumName, params string[] otherAlbumNames) : IFlow<string[]>
{
    public async Task<string[]> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var modal = await albumsPage.OpenMergeAlbumsAsync([keptAlbumName, .. otherAlbumNames]);
        await modal.KeepAsync(keptAlbumName);
        var summary = await modal.GetSummaryAsync();
        await modal.ConfirmAsync();

        // The dialog closes once the merge succeeds, but the list refetch may still be in flight.
        // Wait for the merged rows to actually disappear.
        foreach (var name in otherAlbumNames)
            await Assertions.Expect(albumsPage.Collection.GetCellsByExactText("name", name)).ToHaveCountAsync(0);
        await albumsPage.Collection.WaitForLoadedAsync();

        return summary;
    }
}
