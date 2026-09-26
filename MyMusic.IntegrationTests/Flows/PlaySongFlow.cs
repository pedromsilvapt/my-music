using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Plays a song from the songs page and makes sure it is paused, so the short test file
/// cannot finish (and advance the queue) while the test runs.
/// Returns the footer player showing the song.
/// </summary>
public class PlaySongFlow(string songTitle) : IFlow<FooterPlayerComponent>
{
    public async Task<FooterPlayerComponent> ExecuteAsync(IPage page)
    {
        // Start playing the song from the songs page
        var songsPage = await new HomePage(page).Navbar.GoToSongsAsync();
        await songsPage.Collection.WaitForLoadedAsync();
        await songsPage.Collection.PlaySongByTitleAsync(songTitle);

        // The footer player should show the song; make sure it is paused to keep it as the current song
        var footerPlayer = songsPage.FooterPlayer;
        await footerPlayer.WaitForSongAsync(songTitle);
        await footerPlayer.EnsurePausedAsync();

        return footerPlayer;
    }
}
