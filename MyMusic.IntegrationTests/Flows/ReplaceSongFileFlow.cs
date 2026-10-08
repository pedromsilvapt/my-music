using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a song's edit modal and replaces its audio with the given file, using the "Upload Song" tool. Leaves the
/// edit modal closed, with the song's details page behind it.
/// </summary>
public class ReplaceSongFileFlow(string songTitle, SongFile file) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        // Open the tool from the song's edit modal
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        var editModal = await songDetails.OpenEditModalAsync();
        var uploadModal = await editModal.OpenUploadSongAsync();

        // Choose the file and confirm; the tool reports back in a notification
        await uploadModal.ChooseFileAsync(file);
        await uploadModal.ReplaceAsync();
        await songDetails.Notifications.WaitForMessageAsync("The song's audio has been replaced.");

        // Nothing was edited in the modal itself
        await editModal.CancelAsync();
    }
}
