using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
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

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

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

        // Assert: the first revision has nothing to compare against, and only newer revisions are reachable
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
