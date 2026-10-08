using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// The timestamps a song had when the "Change Timestamps" tool was opened, and the ones it shows after saving.
/// </summary>
public record SongTimestampsChange(
    IReadOnlyDictionary<SongTimestamp, DateTime?> Before,
    IReadOnlyDictionary<SongTimestamp, DateTime?> After);

/// <summary>
/// Opens a song's edit modal, sets the given timestamps to the current time with the "Change Timestamps" tool and
/// saves them. Returns the song's timestamps as the tool showed them before and after the change.
/// </summary>
public class ChangeSongTimestampsFlow(string songTitle, params SongTimestamp[] setToNow) : IFlow<SongTimestampsChange>
{
    public async Task<SongTimestampsChange> ExecuteAsync(IPage page)
    {
        // Open the tool from the song's edit modal
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        var editModal = await songDetails.OpenEditModalAsync();
        var timestampsModal = await editModal.OpenChangeTimestampsAsync();
        var before = await timestampsModal.GetValuesAsync();

        // Set the timestamps to now and save; the tool reports back in a notification
        foreach (var timestamp in setToNow)
        {
            await timestampsModal.SetNowAsync(timestamp);
        }

        await timestampsModal.SaveAsync();
        await songDetails.Notifications.WaitForMessageAsync("The timestamps have been updated.");

        // Reopen the tool to read the timestamps the song has now
        timestampsModal = await editModal.OpenChangeTimestampsAsync();
        var after = await timestampsModal.GetValuesAsync();
        await timestampsModal.CancelAsync();

        return new SongTimestampsChange(before, after);
    }
}
