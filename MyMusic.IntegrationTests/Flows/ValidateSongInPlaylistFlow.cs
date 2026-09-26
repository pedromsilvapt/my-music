using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a playlist's detail page by name and asserts whether a song with the given title is listed in it.
/// </summary>
public class ValidateSongInPlaylistFlow(string songTitle, string playlistName, bool shouldExist = true) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var details = await new OpenPlaylistDetailsFlow(playlistName).ExecuteAsync(page);

        var exists = await details.Songs.FindRowByTitleAsync(songTitle) >= 0;
        exists.ShouldBe(shouldExist,
            $"Song '{songTitle}' should {(shouldExist ? "" : "not ")}be in playlist '{playlistName}'");
    }
}
