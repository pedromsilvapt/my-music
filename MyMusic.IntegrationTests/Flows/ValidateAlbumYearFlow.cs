using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the album's detail page through the Albums list and asserts the year it displays.
/// </summary>
public class ValidateAlbumYearFlow(string albumName, int year) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();
        var albumDetails = await albumsPage.GoToAlbumDetailsAsync(albumName);

        await Assertions.Expect(albumDetails.Year).ToHaveTextAsync(year.ToString());
    }
}
