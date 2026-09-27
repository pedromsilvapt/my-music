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

    [Fact]
    public async Task GoTo_ThenTrackChange_ShouldScrollToNewCurrentSong()
    {
        // Seed enough songs for the queue to overflow the viewport, and queue them all
        await _songs.SeedAsync(RequestContext, UserId);
        var nowPlaying = await new OpenNowPlayingFlow().ExecuteAsync(Page);
        var collection = nowPlaying.PlayerPage.Collection;

        // Jump to a song far down the queue; its flash should end, leaving the current song off screen
        await collection.GoToAsync(FarDownSongTitle);
        await Assertions.Expect(collection.GetRowByTitle(FarDownSongTitle)).ToHaveAttributeAsync("data-highlighted", "true");
        await Assertions.Expect(collection.HighlightedRows).ToHaveCountAsync(0);

        // Skipping to the next song should still follow the track: scroll back up and flash the new current song
        await nowPlaying.FooterPlayer.NextAsync(nowPlaying.NextTitle);
        var nextRow = collection.GetRowByTitle(nowPlaying.NextTitle);
        await Assertions.Expect(nextRow).ToBeInViewportAsync();
        await Assertions.Expect(nextRow).ToHaveAttributeAsync("data-highlighted", "true");
    }

    [Fact]
    public async Task GoTo_ThenScrollToCurrent_ShouldFlashOnlyCurrentSong()
    {
        // Seed enough songs for the queue to overflow the viewport, and queue them all
        await _songs.SeedAsync(RequestContext, UserId);
        var nowPlaying = await new OpenNowPlayingFlow().ExecuteAsync(Page);
        var collection = nowPlaying.PlayerPage.Collection;

        // Jump to a song far down the queue and let its flash end
        await collection.GoToAsync(FarDownSongTitle);
        await Assertions.Expect(collection.GetRowByTitle(FarDownSongTitle)).ToHaveAttributeAsync("data-highlighted", "true");
        await Assertions.Expect(collection.HighlightedRows).ToHaveCountAsync(0);

        // Clicking the song in the footer player should scroll to the current song and flash it
        await nowPlaying.FooterPlayer.OpenPlayerPageAsync();
        var currentRow = collection.GetRowByTitle(nowPlaying.CurrentTitle);
        await Assertions.Expect(currentRow).ToBeInViewportAsync();
        await Assertions.Expect(currentRow).ToHaveAttributeAsync("data-highlighted", "true");

        // The old GoTo target should not flash again on the way: the current song is the only highlighted row
        (await collection.HighlightedRows.CountAsync()).ShouldBe(1, "Only the current song should be highlighted");
    }
}
