using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Songs;

/// <summary>
/// Integration tests for the "Merge" bulk action of the songs page: merging songs picked by the user, without them
/// having been detected as soundalikes first.
/// </summary>
public class SongsMergeTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

    // Scenario: Songs picked by the user can be merged into one of them
    //   Given two songs by different artists, never detected as soundalikes
    //   When the user selects both and merges them, keeping the first
    //   Then the merge dialog shows how alike they sound, and proposes to merge the other song
    //   And only the kept song is left in the library
    //   And it gained the artist of the merged song
    [Fact]
    public async Task MergeSongs_FromSelection_ShouldMergeTheOtherSongIntoTheKeptOne()
    {
        // Setup: seed two songs from different artists, with no soundalike scan having run on them
        var kept = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan
        var merged = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]); // Freya Ridings

        // Action: select both songs and merge them, keeping the first — the dialog should show how alike they sound
        // and merge the other song by default
        var result = await new MergeSongsFlow(kept.Title, merged.Title).ExecuteAsync(Page);

        // Assert: the dialog showed a match score, and proposed to merge rather than delete
        result.MatchScore.ShouldNotBeNull("The merge dialog should show the songs' match score");
        result.DefaultAction.ShouldBe(SoundalikeAction.Merge);

        // Assert: only the kept song is left, and it gained the merged song's artist
        await new ShouldSongExistFlow(kept.Title, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistFlow(merged.Title, shouldExist: false).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow(kept.Title, new ValidateSongOptions(
            Artists: ["Dylan", "Freya Ridings"]
        )).ExecuteAsync(Page);
    }

    // Scenario: Merging is not offered for a single song
    //   Given two songs exist in the library
    //   When the user opens the actions menu with only one of them selected
    //   Then the menu does not offer to merge songs
    [Fact]
    public async Task MergeAction_WithSingleSongSelected_ShouldNotBeOffered()
    {
        // Setup: seed two songs, so a merge would be possible
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);

        // Action: open the actions menu with a single song selected
        var menu = await new PerformSongsActionFlow(song.Title).ExecuteAsync(Page);

        // Assert: there is nothing to merge a single song with, so the action should not be listed
        (await menu.HasItemAsync(SongsActionsMenuComponent.MergeSongs)).ShouldBeFalse();
    }
}
