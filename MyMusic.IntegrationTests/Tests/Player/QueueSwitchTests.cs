using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Player;

/// <summary>
/// Integration tests for playing songs from a queue other than the one currently playing.
/// </summary>
public class QueueSwitchTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();
    private readonly QueuesFixture _queues = new();

    // Scenario: Playing a song from another queue never flashes the previously playing queue
    //   Given a playing queue with two songs
    //   And another queue with only one of those songs
    //   When the user opens the now playing page
    //   Then the song that is only in the playing queue is listed
    //   When the user views the other queue, without playing it
    //   Then the song that is only in the playing queue is no longer listed
    //   When the user plays a song from the viewed queue
    //   Then the player loads that song
    //   And the list stays on the viewed queue, never showing the song that is only in the previous one
    [Fact]
    public async Task PlaySongFromOtherQueue_ShouldNeverShowPreviousQueue()
    {
        // Seed two queues: "Road Trip" with a single song, then the playing queue with that song and another one
        var onlyInPlayingQueue = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi
        var inBothQueues = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]); // Freya Ridings - Wicker Woman
        var otherQueue = await _queues.SeedAsync(RequestContext, UserId, "Road Trip", inBothQueues);
        await _queues.SeedAsync(RequestContext, UserId, "Everything", onlyInPlayingQueue, inBothQueues);

        // Reload so the app picks up the seeded playing queue, then open the now playing page
        await Page.ReloadAsync();
        var playerPage = await new HomePage(Page).Navbar.GoToPlayerAsync();
        var collection = playerPage.Collection;
        await Assertions.Expect(collection.GetRowByTitle(onlyInPlayingQueue.Title)).ToBeVisibleAsync();

        // View the other queue without playing it: the song only in the playing queue should be gone
        var switcher = await playerPage.OpenQueueSwitcherAsync();
        await switcher.ViewQueueAsync(otherQueue.Name);
        await Assertions.Expect(collection.GetRowByTitle(onlyInPlayingQueue.Title)).ToBeHiddenAsync();

        // Play a song from the viewed queue, recording every song the list renders while it switches queues
        await playerPage.StartRecordingSongTitlesAsync();
        await collection.PlaySongByTitleAsync(inBothQueues.Title);
        await playerPage.FooterPlayer.WaitForSongAsync(inBothQueues.Title);
        await playerPage.FooterPlayer.EnsurePausedAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var renderedTitles = await playerPage.StopRecordingSongTitlesAsync();

        // The list should have stayed on the viewed queue, never jumping back to the previously playing one
        renderedTitles.ShouldContain(inBothQueues.Title);
        renderedTitles.ShouldNotContain(onlyInPlayingQueue.Title, "The previously playing queue should never be shown");
        await Assertions.Expect(collection.GetRowByTitle(onlyInPlayingQueue.Title)).ToBeHiddenAsync();
    }
}
