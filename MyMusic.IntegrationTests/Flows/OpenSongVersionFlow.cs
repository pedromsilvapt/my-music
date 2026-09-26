using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the song's detail page, waits until it lists <paramref name="versionsCount"/> history versions,
/// then opens the version at <paramref name="index"/> (0 is the newest) in the version modal.
/// </summary>
public class OpenSongVersionFlow(
    string songTitle,
    int versionsCount,
    int index = 0) : IFlow<SongVersionModalComponent>
{
    public async Task<SongVersionModalComponent> ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        await songDetails.WaitForVersionsCountAsync(versionsCount);

        return await songDetails.OpenVersionAsync(index);
    }
}
