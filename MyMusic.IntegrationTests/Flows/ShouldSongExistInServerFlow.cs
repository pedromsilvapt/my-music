using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

public class ShouldSongExistInServerFlow(string songTitle) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var rowIndex = await songsPage.Collection.FindRowByTitleAsync(songTitle);
        rowIndex.ShouldBeGreaterThanOrEqualTo(0, $"Song '{songTitle}' not found on server");
    }
}
