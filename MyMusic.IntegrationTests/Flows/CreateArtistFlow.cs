using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page and creates an artist through the "New artist" dialog. Returns the artists page,
/// whose list should show the new artist without a reload.
/// </summary>
public class CreateArtistFlow(string name) : IFlow<ArtistsPage>
{
    public async Task<ArtistsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        var modal = await artistsPage.OpenCreateArtistAsync();
        await modal.CreateAsync(name);

        return artistsPage;
    }
}
