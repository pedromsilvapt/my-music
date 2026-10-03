using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;

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

    [Fact]
    public async Task RecalculateChecksum_UnmodifiedFile_ShouldReportChecksumUnchanged()
    {
        // Setup: seed a freshly uploaded song, whose recorded checksum still matches its file
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);

        // Action & Assert: recalculating the checksum from the edit modal's tools menu should report that nothing
        // had to be updated
        await new RecalculateSongChecksumFlow(song.Title, "Nothing changed, checksum was already up to date.").ExecuteAsync(Page);
    }
}
