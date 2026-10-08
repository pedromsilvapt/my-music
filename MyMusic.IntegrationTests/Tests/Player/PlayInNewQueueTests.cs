using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Player;

/// <summary>
/// Integration tests for playing songs in a new queue, leaving the previously playing queue untouched.
/// </summary>
public class PlayInNewQueueTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();
    private readonly QueuesFixture _queues = new();

    // Scenario: Playing a song from its details page starts a new queue, keeping the previous one
    //   Given a playing queue with two songs
    //   And a third song that is not in that queue
    //   When the user opens the third song's details page and plays it
    //   Then the player loads that song
    //   When the user opens the now playing page
    //   Then only the played song is listed
    //   When the user views the previous queue
    //   Then it still has its two original songs
    [Fact]
    public async Task PlaySongFromDetailsPage_ShouldCreateNewQueue()
    {
        // Seed a playing queue with two songs, and a third song outside of it
        var firstQueued = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi
        var secondQueued = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]); // Freya Ridings - Wicker Woman
        var notQueued = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[5]); // Dove Cameron - Sand
        var previousQueue = await _queues.SeedAsync(RequestContext, UserId, "Everything", firstQueued, secondQueued);

        // Reload so the app picks up the seeded playing queue, then play the third song from its details page
        await Page.ReloadAsync();
        var songDetails = await new OpenSongDetailsFlow(notQueued.Title).ExecuteAsync(Page);
        await songDetails.PlayAsync();

        // The player should load the song; keep it paused so it stays the current song
        var footerPlayer = songDetails.FooterPlayer;
        await footerPlayer.WaitForSongAsync(notQueued.Title);
        await footerPlayer.EnsurePausedAsync();

        // The now playing page should show a new queue, with only the played song
        var playerPage = await footerPlayer.OpenPlayerPageAsync();
        var collection = playerPage.Collection;
        await Assertions.Expect(collection.GetRowByTitle(notQueued.Title)).ToBeVisibleAsync();
        await Assertions.Expect(collection.GetRowByTitle(firstQueued.Title)).ToBeHiddenAsync();
        await Assertions.Expect(collection.GetRowByTitle(secondQueued.Title)).ToBeHiddenAsync();

        // The previous queue should still exist, with its two original songs and without the played one
        var switcher = await playerPage.OpenQueueSwitcherAsync();
        await switcher.ViewQueueAsync(previousQueue.Name);
        await Assertions.Expect(collection.GetRowByTitle(firstQueued.Title)).ToBeVisibleAsync();
        await Assertions.Expect(collection.GetRowByTitle(secondQueued.Title)).ToBeVisibleAsync();
        await Assertions.Expect(collection.GetRowByTitle(notQueued.Title)).ToBeHiddenAsync();
    }
}
