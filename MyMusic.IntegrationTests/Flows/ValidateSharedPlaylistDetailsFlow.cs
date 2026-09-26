using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a playlist shared with the current user and asserts it credits the sharer, lists the given songs,
/// and is read-only (no Share button).
/// </summary>
public class ValidateSharedPlaylistDetailsFlow(string playlistName, string sharerName, params string[] songTitles) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var details = await new OpenPlaylistDetailsFlow(playlistName).ExecuteAsync(page);

        (await details.SharedByLabel.InnerTextAsync())
            .ShouldContain(sharerName, customMessage: "Shared playlist should credit the sharer");

        foreach (var songTitle in songTitles)
        {
            (await details.Songs.FindRowByTitleAsync(songTitle))
                .ShouldBeGreaterThanOrEqualTo(0, $"Song '{songTitle}' should be listed in the shared playlist");
        }

        (await details.ShareButton.CountAsync()).ShouldBe(0, "Recipients cannot re-share a playlist");
    }
}
