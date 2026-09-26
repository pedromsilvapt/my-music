using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the playlists page and asserts whether a playlist with the given name is listed
/// (owned by, or shared with, the current user).
/// </summary>
public class ShouldPlaylistExistFlow(string playlistName, bool shouldExist = true) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var playlistsPage = await new HomePage(page).Navbar.GoToPlaylistsAsync();

        var exists = await playlistsPage.HasPlaylistAsync(playlistName);
        exists.ShouldBe(shouldExist, $"Playlist '{playlistName}' should {(shouldExist ? "" : "not ")}be listed");
    }
}
