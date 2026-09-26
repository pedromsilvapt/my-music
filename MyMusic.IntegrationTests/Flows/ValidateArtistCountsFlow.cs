using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the artist's detail page through the Artists list and asserts the song and album counts it displays.
/// </summary>
public class ValidateArtistCountsFlow(string artistName, int songsCount, int albumsCount) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();
        var artistDetails = await artistsPage.GoToArtistDetailsAsync(artistName);

        await Assertions.Expect(artistDetails.SongsCount).ToHaveAttributeAsync("data-count", songsCount.ToString());
        await Assertions.Expect(artistDetails.AlbumsCount).ToHaveAttributeAsync("data-count", albumsCount.ToString());
    }
}
