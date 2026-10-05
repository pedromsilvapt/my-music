using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and asserts how many albums have exactly the given name. Albums of different
/// album artists can share a name.
/// </summary>
public class ValidateAlbumsNamedFlow(string albumName, int count) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        await Assertions.Expect(albumsPage.Collection.GetCellsByExactText("name", albumName)).ToHaveCountAsync(count);
    }
}
