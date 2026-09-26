using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Removes a song from a playlist through the song's Manage Playlists dialog.
/// </summary>
public class RemoveSongFromPlaylistFlow(string songTitle, string playlistName) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var dialog = await new OpenManagePlaylistsDialogFlow(songTitle).ExecuteAsync(page);
        await dialog.SelectPlaylistAsync(playlistName, "remove");
        await dialog.ApplyAsync();
    }
}
