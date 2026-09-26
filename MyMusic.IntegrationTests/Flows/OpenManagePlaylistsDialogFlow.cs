using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the song detail page for the given song and opens its Manage Playlists dialog.
/// </summary>
public class OpenManagePlaylistsDialogFlow(string songTitle) : IFlow<ManagePlaylistsDialogComponent>
{
    public async Task<ManagePlaylistsDialogComponent> ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        var dialog = await songDetails.OpenManagePlaylistsDialogAsync();
        await dialog.WaitForLoadedAsync();
        return dialog;
    }
}
