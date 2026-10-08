using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Songs;

/// <summary>
/// Integration tests for the song tools menu of the edit song modal.
/// </summary>
public class SongsToolsTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

    // Scenario: Recalculating the checksum of an untouched song reports that nothing changed
    //   Given a freshly uploaded song, whose file was not modified since
    //   When the user recalculates its checksum from the tools menu of the edit song dialog
    //   Then a message says the checksum was already up to date
    [Fact]
    public async Task RecalculateChecksum_UnmodifiedFile_ShouldReportChecksumUnchanged()
    {
        // Setup: seed a freshly uploaded song, whose recorded checksum still matches its file
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);

        // Action & Assert: recalculating the checksum from the edit modal's tools menu should report that nothing
        // had to be updated
        await new RecalculateSongChecksumFlow(song.Title, "Nothing changed, checksum was already up to date.").ExecuteAsync(Page);
    }

    // Scenario: Setting a song's creation date to now only changes that date
    //   Given an uploaded song
    //   When the user opens the change timestamps tool from the tools menu of the edit song dialog
    //   And sets the creation date to now and saves
    //   Then a message says the timestamps were updated
    //   When the user opens the change timestamps tool again
    //   Then the creation date is the new one
    //   And the modification, addition and file modification dates are the same as before
    [Fact]
    public async Task ChangeTimestamps_SetCreatedAtToNow_ShouldOnlyChangeCreatedAt()
    {
        // Setup: seed a song, which gets its timestamps when it is uploaded
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);

        // Action: set the creation date to now with the tool; saving should be confirmed in a notification
        var change = await new ChangeSongTimestampsFlow(song.Title, SongTimestamp.CreatedAt).ExecuteAsync(Page);

        // Assert: the creation date should have moved forward, and the other timestamps should be untouched
        change.After[SongTimestamp.CreatedAt].ShouldNotBeNull();
        change.After[SongTimestamp.CreatedAt]!.Value.ShouldBeGreaterThan(change.Before[SongTimestamp.CreatedAt]!.Value);
        change.After[SongTimestamp.ModifiedAt].ShouldBe(change.Before[SongTimestamp.ModifiedAt]);
        change.After[SongTimestamp.AddedAt].ShouldBe(change.Before[SongTimestamp.AddedAt]);
        change.After[SongTimestamp.FileModifiedAt].ShouldBe(change.Before[SongTimestamp.FileModifiedAt]);
    }
}
