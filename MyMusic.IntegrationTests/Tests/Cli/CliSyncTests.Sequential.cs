using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public partial class CliSyncTests
{
    [Fact]
    public async Task Sync_ShouldHandleSuccessiveTitleChangesAcrossSyncs()
    {
        // Seed song on server associated with this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [_cli.DeviceId] }]);

        // First sync - download the song
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();

        // Verify original file exists
        var originalPath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        _cli.FileExists(originalPath).ShouldBeTrue();

        // Change title to "A" on server
        await new EditSongFlow("Sand", new(Title: "Title A")).ExecuteAsync(Page);

        // Second sync - should rename file
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();

        // Verify file was renamed to "Title A"
        var pathA = "Dove Cameron/Sand/Title A - Dove Cameron.mp3";
        _cli.FileExists(originalPath).ShouldBeFalse();
        _cli.FileExists(pathA).ShouldBeTrue();

        // Change title to "B" on server
        await new EditSongFlow("Title A", new(Title: "Title B")).ExecuteAsync(Page);

        // Third sync - should rename file again
        var result3 = await CliRunner.SyncAsync(_cli);
        result3.ShouldBeSuccessful();

        // Verify final state reflects title "B"
        var pathB = "Dove Cameron/Sand/Title B - Dove Cameron.mp3";
        _cli.FileExists(pathA).ShouldBeFalse();
        _cli.FileExists(pathB).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath(pathB), title: "Title B");
    }

    [Fact]
    public async Task Sync_ShouldUploadAndUpdateAndThenDownloadInSuccessiveSyncs()
    {
        // Create a local song
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[1]);

        // First sync - upload the new song
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();
        result1.Created.ShouldBeGreaterThanOrEqualTo(1);

        // Modify the file locally
        var localPath = "The Alibi.mp3";
        await _cli.UpdateLocalFileMetadataAsync(localPath, new(Title: "Updated Alibi"));

        // Second sync - upload the local change
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();
        result2.Updated.ShouldBeGreaterThanOrEqualTo(1);

        // Modify the song on the server
        await new EditSongFlow("Updated Alibi", new(Title: "Server Updated Alibi")).ExecuteAsync(Page);

        // Third sync - download the server change
        var result3 = await CliRunner.SyncAsync(_cli);
        result3.ShouldBeSuccessful();
        result3.Downloaded.ShouldBeGreaterThanOrEqualTo(1);

        // Verify final state reflects the server-side change
        var finalPath = "Dylan/The Alibi/Server Updated Alibi - Dylan.mp3";
        _cli.FileExists(finalPath).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath(finalPath), title: "Server Updated Alibi");
    }
}
