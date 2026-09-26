using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates to the playlists page and asserts each playlist's share indicator state
/// (<see cref="PlaylistShareIndicatorComponent.SharedByMe"/>, <see cref="PlaylistShareIndicatorComponent.SharedWithMe"/>,
/// or <see langword="null"/> when the playlist should not be marked as shared).
/// </summary>
public class ValidatePlaylistShareStateFlow(params (string PlaylistName, string? State)[] expected) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var playlistsPage = await new HomePage(page).Navbar.GoToPlaylistsAsync();

        foreach (var (playlistName, state) in expected)
        {
            (await playlistsPage.ShareIndicator(playlistName).GetStateAsync())
                .ShouldBe(state, $"Playlist '{playlistName}' should have share state '{state ?? "none"}'");
        }
    }
}
