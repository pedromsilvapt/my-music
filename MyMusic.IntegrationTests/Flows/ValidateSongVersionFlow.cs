using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the song's version at <paramref name="index"/> (0 is the newest) once it has
/// <paramref name="versionsCount"/> versions, validates it, and closes the modal.
/// </summary>
public class ValidateSongVersionFlow(
    IAPIRequestContext api,
    long songId,
    string songTitle,
    int versionsCount,
    ValidateCurrentSongVersionOptions expected,
    int index = 0) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var modal = await new OpenSongVersionFlow(api, songId, songTitle, versionsCount, index).ExecuteAsync(page);

        await new ValidateCurrentSongVersionFlow(expected).ExecuteAsync(page);

        await modal.CloseAsync();
    }
}
