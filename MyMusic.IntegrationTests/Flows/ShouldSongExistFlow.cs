using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the songs page and asserts whether a song with the given title exists.
/// </summary>
public class ShouldSongExistFlow(string songTitle, bool shouldExist) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var rowIndex = await songsPage.Collection.FindRowByTitleAsync(songTitle);
        var exists = rowIndex >= 0;
        exists.ShouldBe(shouldExist, $"Song '{songTitle}' should {(shouldExist ? "" : "not ")}exist");
    }
}
