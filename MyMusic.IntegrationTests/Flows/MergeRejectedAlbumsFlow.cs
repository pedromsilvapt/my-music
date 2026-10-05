using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page, selects <paramref name="keptAlbumName"/> and <paramref name="otherAlbumNames"/>
/// and tries to merge them keeping the former, which the server should not allow. Returns the reason the dialog
/// shows, while staying open with the merge disabled.
/// </summary>
public class MergeRejectedAlbumsFlow(string keptAlbumName, params string[] otherAlbumNames) : IFlow<string>
{
    public async Task<string> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var modal = await albumsPage.OpenMergeAlbumsAsync([keptAlbumName, .. otherAlbumNames]);
        await modal.KeepAsync(keptAlbumName);

        return await modal.GetRejectionAsync();
    }
}
