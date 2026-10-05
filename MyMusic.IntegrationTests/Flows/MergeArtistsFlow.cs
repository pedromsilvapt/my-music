using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page, selects <paramref name="keptArtistName"/> and <paramref name="otherArtistNames"/>
/// and merges them through the selection's Actions menu, keeping the former. Returns the sentences the dialog
/// showed about what the merge changes, right before it was confirmed. The other artists must not share their name
/// with the kept one.
/// </summary>
public class MergeArtistsFlow(string keptArtistName, params string[] otherArtistNames) : IFlow<string[]>
{
    public async Task<string[]> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        var modal = await artistsPage.OpenMergeArtistsAsync([keptArtistName, .. otherArtistNames]);
        await modal.KeepAsync(keptArtistName);
        var summary = await modal.GetSummaryAsync();
        await modal.ConfirmAsync();

        // The dialog closes once the merge succeeds, but the list refetch may still be in flight.
        // Wait for the merged rows to actually disappear.
        foreach (var name in otherArtistNames)
            await Assertions.Expect(artistsPage.Collection.GetCellsByExactText("name", name)).ToHaveCountAsync(0);
        await artistsPage.Collection.WaitForLoadedAsync();

        return summary;
    }
}
