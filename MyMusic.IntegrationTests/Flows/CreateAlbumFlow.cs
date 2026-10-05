using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and creates an album through the "New album" dialog, picking its artist by name.
/// Returns the albums page, whose list should show the new album without a reload.
/// </summary>
public class CreateAlbumFlow(string name, string artistName, int? year = null) : IFlow<AlbumsPage>
{
    public async Task<AlbumsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var modal = await albumsPage.OpenCreateAlbumAsync();
        await modal.FillAsync(name, artistName, year);
        await modal.CreateAsync();

        return albumsPage;
    }
}
