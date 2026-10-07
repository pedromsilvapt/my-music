using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Tests.SongHistory;

/// <summary>
/// Integration tests for song history tracking: editing a song should record a new version,
/// visible from the song detail page's versions menu, showing only the fields that changed.
/// </summary>
public class SongHistoryTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;
    private SoundalikesFixture _soundalikes = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
        _soundalikes = new SoundalikesFixture();
    }

    // Scenario: Renaming a song records a new version showing the title change
    //   Given a song exists with only its initial version
    //   When the user changes the song's title
    //   Then the song has a second version
    //   And that version shows the title going from the old value to the new one
    [Fact]
    public async Task EditTitle_ShouldRecordTitleChangeAsNewVersion()
    {
        // Setup: seed a song whose initial (upload) version has already been recorded
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 1 });

        // Action: rename the song through the edit modal — should record a second version
        await new EditSongFlow(song.Title, new(Title: "The Alibi (Edited)")).ExecuteAsync(Page);

        // Assert: the newest version shows the title changing from the old value to the new one
        await new ValidateSongVersionFlow("The Alibi (Edited)", versionsCount: 2, new(
            Old: new() { Title = "The Alibi" },
            New: new() { Title = "The Alibi (Edited)" }))
            .ExecuteAsync(Page);
    }

    // Scenario: A freshly uploaded song has a first version holding its full state
    //   Given a song was uploaded and never edited
    //   When the user opens the song's only version
    //   Then it is shown as the first revision, with the song's title and artists
    //   And there are no previous values to compare against
    [Fact]
    public async Task Upload_ShouldRecordFullStateAsCreatedBaseline()
    {
        // Setup: seed a freshly uploaded song, with no edits yet
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 1 });

        // Action: open its only version from the versions menu
        await new OpenSongVersionFlow(song.Title, versionsCount: 1).ExecuteAsync(Page);

        // Assert: the upload is recorded as the created baseline, showing the song's full state (including the
        // artists inserted along with it) and no previous values
        await new ValidateCurrentSongVersionFlow(new(
            Revision: 1,
            HasOldPanel: false,
            New: new() { Title = "The Alibi", Artists = [new() { Name = "Dylan" }] }))
            .ExecuteAsync(Page);
    }

    // Scenario: The versions menu picks up a new version without reloading the page
    //   Given a song exists with only its initial version
    //   When the user changes the song's title
    //   And stays on the song's page
    //   Then the versions menu ends up listing two versions
    [Fact]
    public async Task EditTitle_ShouldUpdateVersionsMenuWithoutReload()
    {
        // Setup: seed a song whose initial (upload) version has already been recorded
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 1 });

        // Action: rename the song through the edit modal — the new version is queued for background processing
        await new EditSongFlow(song.Title, new(Title: "The Alibi (Edited)")).ExecuteAsync(Page);

        // Assert: staying on the same page, the versions menu should pick up the second version once it is
        // processed, and the pending indicator should go away
        await new SongDetailsPage(Page).WaitForVersionsCountAsync(2);
    }

    // Scenario: Adding an artist to a song records a new version showing the artists change
    //   Given a song with a single artist exists with only its initial version
    //   When the user adds a second artist to the song
    //   Then the song has a second version
    //   And that version shows the artists list gaining the new artist
    [Fact]
    public async Task EditArtists_ShouldRecordArtistsChangeAsNewVersion()
    {
        // Setup: seed a single-artist song (Dylan - The Alibi) whose initial (upload) version has already been recorded
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 1 });

        // Action: add a second artist through the edit modal — should record a second version
        await new EditSongFlow(song.Title, new(Artists: ["Dylan", "Freya Ridings"])).ExecuteAsync(Page);

        // Assert: the newest version shows the artists list gaining the new artist
        await new ValidateSongVersionFlow(song.Title, versionsCount: 2, new(
            Old: new() { Artists = [new() { Name = "Dylan" }] },
            New: new() { Artists = [new() { Name = "Dylan" }, new() { Name = "Freya Ridings" }] }))
            .ExecuteAsync(Page);
    }

    // Scenario: Merging a soundalike into a song records a new version of the kept song
    //   Given two songs with the same audio are detected as soundalikes
    //   And the song to keep has only its initial version
    //   When the user resolves the group, merging the other song into the kept one
    //   Then the kept song has a second version
    //   And that version shows the song gaining the merged song, and how it was merged
    [Fact]
    public async Task ResolveSoundalikes_ShouldRecordMergedSongAsNewVersion()
    {
        // Setup: seed two soundalikes (same audio under different tags) and have them detected as a group; the
        // one to keep already has its initial (upload) version recorded
        var kept = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 1 });
        var merged = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);
        await _soundalikes.SeedAsync(RequestContext);

        // Action: resolve the group from the audits page, merging the other song into the kept one — the merged
        // song is deleted, and the whole resolution should record a single second version of the kept song
        await new ResolveSoundalikesFlow(kept.Title, new() { [merged.Title] = SoundalikeAction.Merge })
            .ExecuteAsync(Page);

        // Assert: the newest version shows the kept song gaining the merged song's id, along with how it was merged
        await new ValidateSongVersionFlow(kept.Title, versionsCount: 2, new(
            Old: new() { MergedSongs = [] },
            New: new() { MergedSongs = [new() { Id = merged.Id, Kind = "SoundalikeMerge" }] }))
            .ExecuteAsync(Page);
    }

    // Scenario: Playing a song does not record a new version
    //   Given a song exists with only its initial version
    //   When the user plays the song through to the end
    //   Then the play shows up in the listening history
    //   And the song still has only its initial version
    [Fact]
    public async Task PlaySong_ShouldNotRecordNewVersion()
    {
        // Setup: seed a song whose initial (upload) version has already been recorded
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 1 });

        // Action: play the song through to the end — should count as a play, bumping its play count
        var footerPlayer = await new PlaySongFlow(song.Title).ExecuteAsync(Page);
        await footerPlayer.PlayAsync();
        await footerPlayer.WaitForPlaybackToEndAsync();

        // Assert: the play was recorded in the listening history
        await new ShouldSongExistInPlayHistoryFlow(song.Title).ExecuteAsync(Page);

        // Assert: a play count change is not an edit, so the song should still have only its initial version
        var songDetails = await footerPlayer.GoToDetailsAsync();
        await songDetails.ShouldHaveSettledVersionsCountAsync(1);
    }

    // Scenario: The user can step back and forth between a song's versions
    //   Given a song exists with three versions
    //   When the user opens the newest version
    //   Then the third revision is shown, compared against the previous one
    //   And only older revisions can be reached
    //   When the user steps back twice
    //   Then the first revision is shown, with nothing to compare against
    //   And only newer revisions can be reached
    //   When the user steps forward once
    //   Then the second revision is shown, with both directions reachable
    //   When the user closes the version
    //   Then it is dismissed
    [Fact]
    public async Task VersionModal_ShouldNavigateBetweenRevisions()
    {
        // Setup: seed a song with three recorded versions
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1] with { VersionsCount = 3 });

        // Action: open the newest version from the versions menu
        var modal = await new OpenSongVersionFlow(song.Title, versionsCount: 3).ExecuteAsync(Page);

        // Assert: revision 3 is shown, comparing against the previous one; only older revisions are reachable
        await new ValidateCurrentSongVersionFlow(new(
            Revision: 3, HasOldPanel: true, CanGoToPrevious: true, CanGoToNext: false))
            .ExecuteAsync(Page);

        // Action: step back twice — should land on the first revision
        await modal.GoToPreviousAsync();
        await modal.GoToPreviousAsync();

        // Assert: the first revision is the created baseline, with nothing to compare against, and only newer
        // revisions are reachable
        await new ValidateCurrentSongVersionFlow(new(
            Revision: 1, HasOldPanel: false, CanGoToPrevious: false, CanGoToNext: true))
            .ExecuteAsync(Page);

        // Action: step forward again — should return to revision 2, with both directions reachable
        await modal.GoToNextAsync();
        await new ValidateCurrentSongVersionFlow(new(
            Revision: 2, HasOldPanel: true, CanGoToPrevious: true, CanGoToNext: true))
            .ExecuteAsync(Page);

        // Close the modal — it should dismiss cleanly
        await modal.CloseAsync();
    }
}
