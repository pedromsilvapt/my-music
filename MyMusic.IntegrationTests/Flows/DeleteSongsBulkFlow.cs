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
        var menu = await new PerformSongsActionFlow(songTitles).ExecuteAsync(page);

        await menu.DeleteAsync();

        var confirmDialog = new ConfirmDialogComponent(page.GetByRole(AriaRole.Dialog));
        await confirmDialog.WaitForVisibleAsync();
        await confirmDialog.ConfirmAsync();

        var songsPage = new SongsPage(page);
        await songsPage.Collection.WaitForLoadedAsync();
    }
}
