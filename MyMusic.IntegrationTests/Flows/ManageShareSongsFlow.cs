using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Selects one or more owned songs, opens the floating Actions menu, clicks the
/// "Share Song" / "Share N Songs" item, opens the Manage Sharing dialog, selects a
/// recipient (with the given action: "add" to share or "remove" to revoke), and applies.
/// </summary>
public class ManageShareSongsFlow(string[] songTitles, string recipientUsername, string action = "add") : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        // Select the songs and open the floating Actions menu
        var menu = await new PerformSongsActionFlow(songTitles).ExecuteAsync(page);

        // Click the "Share Song" / "Share N Songs" menu item (matches both singular and plural)
        await menu.ShareAsync();

        // In the dialog, select the recipient (add or remove) and apply
        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();
        var sharingDialog = new ManageSharingDialogComponent(dialog);
        await sharingDialog.SelectRecipientAsync(recipientUsername, action);
        await sharingDialog.ApplyAsync();
    }
}