using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Player;

/// <summary>
/// Integration tests for the actions menu of the song currently shown in the footer player.
/// </summary>
public class FooterPlayerMenuTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

    [Fact]
    public async Task StopAfterThisSong_ShouldToggleFlagOnCurrentSong()
    {
        // Seed a song and start playing it, so it shows up in the footer player
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi
        var footerPlayer = await new PlaySongFlow(song.Title).ExecuteAsync(Page);

        // Enable "Stop After This Song" from the footer; the queue should flag the song
        await footerPlayer.ToggleStopAfterThisSongAsync();
        await new ShouldSongStopAfterPlaybackFlow(song.Title, shouldStop: true).ExecuteAsync(Page);

        // Toggle it again from the footer; the flag should be cleared
        await footerPlayer.ToggleStopAfterThisSongAsync();
        await new ShouldSongStopAfterPlaybackFlow(song.Title, shouldStop: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task GoToDetails_ShouldOpenSongDetailsPage()
    {
        // Seed a song and start playing it, so it shows up in the footer player
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi
        var footerPlayer = await new PlaySongFlow(song.Title).ExecuteAsync(Page);

        // "Go to Details" should open the details page of the playing song
        var songDetails = await footerPlayer.GoToDetailsAsync();
        (await songDetails.GetTitleAsync()).ShouldBe(song.Title);
    }

    [Fact]
    public async Task FooterMenu_ShouldOnlyShowActionsForCurrentSong()
    {
        // Seed a song and start playing it, so it shows up in the footer player
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi
        var footerPlayer = await new PlaySongFlow(song.Title).ExecuteAsync(Page);

        // The menu should offer details and stop-after-playback, but no actions that only apply to queued songs
        var menu = await footerPlayer.OpenActionsMenuAsync();
        (await menu.HasItemAsync(SongsActionsMenuComponent.GoToDetails)).ShouldBeTrue();
        (await menu.HasItemAsync(SongsActionsMenuComponent.StopAfterThisSong)).ShouldBeTrue();
        (await menu.HasItemAsync(SongsActionsMenuComponent.QueueOnlyActions)).ShouldBeFalse();
    }
}
