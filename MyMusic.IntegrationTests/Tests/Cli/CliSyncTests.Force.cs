using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public partial class CliSyncTests
{
    [Fact]
    public async Task Sync_WithForce_ShouldReUploadUnchangedFiles()
    {
        // Create a local song and sync normally
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[11]);

        // First sync should upload the song
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();
        result1.Created.ShouldBeGreaterThanOrEqualTo(1);

        // Second sync without force should be idempotent (no changes)
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();
        result2.TotalChanges.ShouldBe(0);

        // Sync with force should re-upload the unchanged file
        var result3 = await CliRunner.SyncAsync(_cli, force: true);
        result3.ShouldBeSuccessful();
        result3.Updated.ShouldBeGreaterThanOrEqualTo(1);
    }
}