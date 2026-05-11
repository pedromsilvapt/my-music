using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the albums page and asserts whether an album with the given name exists.
/// </summary>
public class ShouldAlbumExistFlow(string albumName, bool shouldExist = true) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();

        var exists = await albumsPage.Collection.HasItemByTextAsync(albumName);
        exists.ShouldBe(shouldExist, $"Album '{albumName}' should {(shouldExist ? "" : "not ")}exist");
    }
}
