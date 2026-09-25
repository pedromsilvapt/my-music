using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Waits until the song has <paramref name="versionsCount"/> history versions, then opens its detail page
/// and the version at <paramref name="index"/> (0 is the newest) in the version modal.
/// </summary>
public class OpenSongVersionFlow(
    IAPIRequestContext api,
    long songId,
    string songTitle,
    int versionsCount,
    int index = 0) : IFlow<SongVersionModalComponent>
{
    public async Task<SongVersionModalComponent> ExecuteAsync(IPage page)
    {
        // Use the API to wait for the background task to create the history.
        // Exception to the rule of always using the UI just like a user, because for the tests, reloading the page multiple times is too expensive.
        await SongsFixture.WaitForVersionsCountAsync(api, songId, versionsCount);

        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        return await songDetails.OpenVersionAsync(index);
    }
}
