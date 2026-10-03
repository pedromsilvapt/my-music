using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a song's edit modal and runs the "Recalculate Checksum" tool, waiting for the notification that reports
/// its outcome.
/// </summary>
public class RecalculateSongChecksumFlow(string songTitle, string expectedNotification) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        // Open the song's edit modal
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        var editModal = await songDetails.OpenEditModalAsync();

        // Run the tool; it works in the background and reports back in a notification
        await editModal.RecalculateChecksumAsync();
        await songDetails.Notifications.WaitForMessageAsync(expectedNotification);
    }
}
