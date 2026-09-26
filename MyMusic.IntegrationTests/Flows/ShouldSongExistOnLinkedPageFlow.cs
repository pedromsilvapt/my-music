using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a song's detail page (from the shared songs view when <paramref name="sharerId"/> is given), follows its
/// album or artist link, and asserts whether the song is listed on the linked page.
/// </summary>
public class ShouldSongExistOnLinkedPageFlow(
    string songTitle,
    SongLinkedTarget target,
    long? sharerId = null,
    bool shouldExist = true) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var linked = await new OpenSongLinkedDetailsFlow(songTitle, target, sharerId: sharerId).ExecuteAsync(page);
        var songs = target == SongLinkedTarget.Album ? linked.Album!.Songs : linked.Artist!.Songs;

        var exists = await songs.FindRowByTitleAsync(songTitle) >= 0;
        exists.ShouldBe(shouldExist, $"Song '{songTitle}' should {(shouldExist ? "" : "not ")}appear on its linked {target} page");
    }
}
