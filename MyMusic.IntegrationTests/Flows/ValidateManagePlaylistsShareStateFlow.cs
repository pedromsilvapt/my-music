using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the Manage Playlists dialog for the given song, asserts each playlist's share indicator state
/// (<see langword="null"/> when the playlist should not be marked as shared), then cancels the dialog.
/// </summary>
public class ValidateManagePlaylistsShareStateFlow(string songTitle, params (string PlaylistName, string? State)[] expected)
    : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var dialog = await new OpenManagePlaylistsDialogFlow(songTitle).ExecuteAsync(page);

        foreach (var (playlistName, state) in expected)
        {
            (await dialog.ShareIndicator(playlistName).GetStateAsync())
                .ShouldBe(state, $"Playlist '{playlistName}' should have share state '{state ?? "none"}' in the Manage Playlists dialog");
        }

        await dialog.CancelAsync();
    }
}
