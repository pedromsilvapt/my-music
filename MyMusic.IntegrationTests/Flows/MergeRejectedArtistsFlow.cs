using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page, selects <paramref name="keptArtistName"/> and <paramref name="otherArtistNames"/>
/// and tries to merge them keeping the former, which the server should not allow. Returns the reason the dialog
/// shows, while staying open with the merge disabled.
/// </summary>
public class MergeRejectedArtistsFlow(string keptArtistName, params string[] otherArtistNames) : IFlow<string>
{
    public async Task<string> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        var modal = await artistsPage.OpenMergeArtistsAsync([keptArtistName, .. otherArtistNames]);
        await modal.KeepAsync(keptArtistName);

        return await modal.GetRejectionAsync();
    }
}
