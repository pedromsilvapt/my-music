using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the artists page and asserts whether an artist with the given name exists.
/// </summary>
public class ShouldArtistExistFlow(string artistName, bool shouldExist = true) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var artistsPage = await home.Navbar.GoToArtistsAsync();

        var exists = await artistsPage.Collection.HasItemByTextAsync(artistName);
        exists.ShouldBe(shouldExist, $"Artist '{artistName}' should {(shouldExist ? "" : "not ")}exist");
    }
}
