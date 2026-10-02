using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the listening history page and asserts that a play of the song with the given title was recorded.
/// </summary>
public class ShouldSongExistInPlayHistoryFlow(string songTitle) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var historyPage = await new HomePage(page).Navbar.GoToHistoryAsync();
        var exists = await historyPage.Collection.HasItemByTextAsync(songTitle);
        exists.ShouldBeTrue($"A play of song '{songTitle}' should have been recorded");
    }
}
