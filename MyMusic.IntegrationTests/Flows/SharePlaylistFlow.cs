using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a playlist's detail page, clicks "Share", selects a recipient with the given action
/// (<see cref="ShareAction.Add"/> to share or <see cref="ShareAction.Remove"/> to revoke) in the
/// Share Playlists dialog, and applies.
/// </summary>
public class SharePlaylistFlow(string playlistName, string recipientUsername, ShareAction action = ShareAction.Add) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var details = await new OpenPlaylistDetailsFlow(playlistName).ExecuteAsync(page);

        var sharingDialog = await details.OpenShareDialogAsync();
        await sharingDialog.SelectRecipientAsync(recipientUsername, action);
        await sharingDialog.ApplyAsync();
    }
}
