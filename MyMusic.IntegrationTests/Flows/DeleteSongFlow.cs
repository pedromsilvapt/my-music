using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a song's detail page, clicks Delete, and confirms the deletion.
/// </summary>
public class DeleteSongFlow(string songTitle) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        // Open the song detail page
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);

        // Click Delete and confirm
        var confirmDialog = await songDetails.ClickDeleteButtonAsync();
        await confirmDialog.ConfirmAsync();

        // Wait for the navigation back to songs page after deletion
        await page.WaitForURLAsync("**/songs");

        // Wait for the songs collection to load
        var songsPage = new SongsPage(page);
        await songsPage.Collection.WaitForLoadedAsync();
    }
}
