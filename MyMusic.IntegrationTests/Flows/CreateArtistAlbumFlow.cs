using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the artist's detail page through the Artists list and creates an album through its "New album" dialog,
/// which should open with the artist already picked. Returns the artist's detail page, which should show the new
/// album without a reload.
/// </summary>
public class CreateArtistAlbumFlow(string artistName, string name, int? year = null) : IFlow<ArtistDetailsPage>
{
    public async Task<ArtistDetailsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();
        var details = await artistsPage.GoToArtistDetailsAsync(artistName);

        var modal = await details.OpenCreateAlbumAsync();
        await modal.Artist.WaitForValueAsync(artistName);

        await modal.FillAsync(name, year: year);
        await modal.CreateAsync();
        await details.WaitForLoadedAsync();

        return details;
    }
}
