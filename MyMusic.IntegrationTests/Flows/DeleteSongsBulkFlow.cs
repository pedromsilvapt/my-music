using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Selects multiple songs on the songs list page and deletes them via the bulk action.
/// </summary>
public class DeleteSongsBulkFlow(params string[] songTitles) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        await new PerformSongsActionFlow(songTitles).ExecuteAsync(page);

        var deleteMenuItem = page.GetByText($"Delete {songTitles.Length} Songs");
        await deleteMenuItem.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await deleteMenuItem.ClickAsync();

        var confirmDialog = new ConfirmDialogComponent(page.GetByRole(AriaRole.Dialog));
        await confirmDialog.WaitForVisibleAsync();
        await confirmDialog.ConfirmAsync();

        var songsPage = new SongsPage(page);
        await songsPage.Collection.WaitForLoadedAsync();
    }
}
