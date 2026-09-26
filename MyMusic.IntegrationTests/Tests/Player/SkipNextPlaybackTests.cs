using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Player;

/// <summary>
/// Integration tests for the "Skip This Song" flag of songs in the queue.
/// </summary>
public class SkipNextPlaybackTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

    [Fact]
    public async Task SkippedLastSong_ShouldStopPlaybackInsteadOfPlayingIt()
    {
        // Seed two songs; the songs page sorts by title, so "The Alibi" is queued before "Wicker Woman"
        var first = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi
        var last = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]); // Freya Ridings - Wicker Woman

        // Play the first song, which queues both songs
        var footerPlayer = await new PlaySongFlow(first.Title).ExecuteAsync(Page);

        // Flag the last song in the queue to be skipped from the now playing page
        var playerPage = await footerPlayer.OpenPlayerPageAsync();
        await playerPage.Collection.ToggleSkipThisSongAsync(last.Title);
        await Assertions.Expect(playerPage.Collection.GetSkipNextPlaybackIndicator(last.Title)).ToBeVisibleAsync();

        // Let the first song play until the end; playback should stop instead of moving on to the skipped song
        await footerPlayer.PlayAsync();
        await footerPlayer.WaitForPlaybackToEndAsync();

        // The player should stay on the first song, paused, without loading the skipped one
        await footerPlayer.WaitForSongAsync(first.Title);
        await footerPlayer.EnsurePausedAsync();
        await footerPlayer.ShouldNotShowSongAsync(last.Title);
    }
}
