using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Player;

/// <summary>
/// Integration tests for how the now playing page scrolls to, and highlights, the current song.
/// </summary>
public class NowPlayingScrollTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();

    // Last of the default songs, far enough down the queue to be off screen initially
    private const string FarDownSongTitle = "Two Faced";

    // Scenario: The now playing page follows the track after the user jumped elsewhere in the queue
    //   Given a queue long enough to overflow the screen is playing
    //   And the user jumped to a song far down the queue, leaving the current song off screen
    //   When the user skips to the next song
    //   Then the page scrolls back to the new current song and highlights it
    [Fact]
    public async Task GoTo_ThenTrackChange_ShouldScrollToNewCurrentSong()
    {
        // Seed enough songs for the queue to overflow the viewport, and queue them all
        await _songs.SeedAsync(RequestContext, UserId);
        var nowPlaying = await new OpenNowPlayingFlow().ExecuteAsync(Page);
        var collection = nowPlaying.PlayerPage.Collection;

        // Jump to a song far down the queue; its flash should end, leaving the current song off screen
        await collection.GoToAsync(FarDownSongTitle);
        await collection.WaitForScrollRequestFinishedAsync();

        // Skipping to the next song should still follow the track: scroll back up and flash the new current song
        await nowPlaying.FooterPlayer.NextAsync(nowPlaying.NextTitle);
        var nextRow = collection.GetRowByTitle(nowPlaying.NextTitle);
        await Assertions.Expect(nextRow).ToBeInViewportAsync();
        await Assertions.Expect(nextRow).ToHaveAttributeAsync("data-highlighted", "true");
    }

    // Scenario: Going back to the current song highlights it alone
    //   Given a queue long enough to overflow the screen is playing
    //   And the user jumped to a song far down the queue, leaving the current song off screen
    //   When the user clicks the playing song in the footer player
    //   Then the page scrolls to the current song and highlights it
    //   And the song jumped to earlier is not highlighted again
    [Fact]
    public async Task GoTo_ThenScrollToCurrent_ShouldFlashOnlyCurrentSong()
    {
        // Seed enough songs for the queue to overflow the viewport, and queue them all
        await _songs.SeedAsync(RequestContext, UserId);
        var nowPlaying = await new OpenNowPlayingFlow().ExecuteAsync(Page);
        var collection = nowPlaying.PlayerPage.Collection;

        // Jump to a song far down the queue and let its flash end
        await collection.GoToAsync(FarDownSongTitle);
        await collection.WaitForScrollRequestFinishedAsync();

        // Clicking the song in the footer player should scroll to the current song and flash it
        await nowPlaying.FooterPlayer.OpenPlayerPageAsync();
        var currentRow = collection.GetRowByTitle(nowPlaying.CurrentTitle);
        await Assertions.Expect(currentRow).ToBeInViewportAsync();
        await Assertions.Expect(currentRow).ToHaveAttributeAsync("data-highlighted", "true");

        // The old GoTo target should not flash again on the way: the current song is the only highlighted row
        (await collection.HighlightedRows.CountAsync()).ShouldBe(1, "Only the current song should be highlighted");
    }
}
