using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a playlist's detail page (owned or shared with the current user) by navigating to the Playlists page
/// and clicking the playlist's name.
/// </summary>
public class OpenPlaylistDetailsFlow(string playlistName) : IFlow<PlaylistDetailsPage>
{
    public async Task<PlaylistDetailsPage> ExecuteAsync(IPage page)
    {
        var playlistsPage = await new HomePage(page).Navbar.GoToPlaylistsAsync();
        return await playlistsPage.GoToPlaylistDetailsAsync(playlistName);
    }
}
