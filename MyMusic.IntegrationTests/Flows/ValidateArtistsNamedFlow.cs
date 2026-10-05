using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page and asserts how many artists have exactly the given name. Artist names are not
/// unique.
/// </summary>
public class ValidateArtistsNamedFlow(string artistName, int count) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        await Assertions.Expect(artistsPage.Collection.GetCellsByExactText("name", artistName)).ToHaveCountAsync(count);
    }
}
