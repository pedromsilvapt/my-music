using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the now playing page from the footer player and validates whether the song
/// shows the "stop after playback" indicator in the queue.
/// </summary>
public class ShouldSongStopAfterPlaybackFlow(string songTitle, bool shouldStop) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var playerPage = await new HomePage(page).FooterPlayer.OpenPlayerPageAsync();
        var indicator = playerPage.Collection.GetStopAfterPlaybackIndicator(songTitle);

        if (shouldStop)
        {
            await Assertions.Expect(indicator).ToBeVisibleAsync();
        }
        else
        {
            await Assertions.Expect(indicator).ToBeHiddenAsync();
        }
    }
}
