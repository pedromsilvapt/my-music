using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// The now playing page with the whole song list queued and its first song current (and paused).
/// </summary>
public record NowPlayingState(
    PlayerPage PlayerPage,
    FooterPlayerComponent FooterPlayer,
    string CurrentTitle,
    string NextTitle);

/// <summary>
/// Plays the first song of the songs page, which queues the whole list, and opens the now playing page.
/// </summary>
public class OpenNowPlayingFlow : IFlow<NowPlayingState>
{
    public async Task<NowPlayingState> ExecuteAsync(IPage page)
    {
        // Remember the first two songs of the list: the current song and the next one in the queue
        var songsPage = await new HomePage(page).Navbar.GoToSongsAsync();
        await songsPage.Collection.WaitForLoadedAsync();
        var currentTitle = await songsPage.Collection.GetCellValueAsync(0, "title");
        var nextTitle = await songsPage.Collection.GetCellValueAsync(1, "title");

        // Play the first song and open the now playing page from the footer player
        var footerPlayer = await new PlaySongFlow(currentTitle).ExecuteAsync(page);
        var playerPage = await footerPlayer.OpenPlayerPageAsync();

        return new NowPlayingState(playerPage, footerPlayer, currentTitle, nextTitle);
    }
}
