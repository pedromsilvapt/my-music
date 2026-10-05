using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and tries to rename an album to a name the server should reject. Returns the error
/// the editor shows, while staying open.
/// </summary>
public class EditRejectedAlbumFlow(string albumName, string newName) : IFlow<string>
{
    public async Task<string> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var modal = await albumsPage.OpenEditAlbumAsync(albumName);
        await modal.FillAsync(newName);

        return await modal.SaveRejectedAsync();
    }
}
