using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and tries to create an album the server should reject. Returns the error the
/// "New album" dialog shows, while staying open.
/// </summary>
public class CreateRejectedAlbumFlow(string name, string artistName) : IFlow<string>
{
    public async Task<string> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var modal = await albumsPage.OpenCreateAlbumAsync();
        await modal.FillAsync(name, artistName);

        return await modal.CreateRejectedAsync();
    }
}
