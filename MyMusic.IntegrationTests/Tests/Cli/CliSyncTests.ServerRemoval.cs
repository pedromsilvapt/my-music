using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public partial class CliSyncTests
{
    [Fact]
    public async Task Sync_ShouldDeleteLocalFileWhenServerMarksForRemoval()
    {
        // Seed song on server associated with this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [_cli.DeviceId] }]);

        // Run sync to download the song to the device
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();

        // Verify file exists locally
        var expectedPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        _cli.FileExists(expectedPath).ShouldBeTrue();

        // Mark device for removal on the server
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[0].Id, _cli.DeviceId);

        // Run sync again - should delete the local file
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();

        // Verify local file was removed
        _cli.FileExists(expectedPath).ShouldBeFalse();

        // Verify the song-device association was removed
        await new ShouldSongExistInDeviceFlow("Wicker Woman", _cli.DeviceName, shouldExist: false)
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task Sync_ShouldRemoveLocalFileWhenServerSongDeleted()
    {
        // Seed song on server associated with this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[3] with { DeviceIds = [_cli.DeviceId] }]);

        // Run sync to download the song to the device
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();

        // Verify file exists locally
        var expectedPath = "Taylor Swift/The Life of a Showgirl/The Fate of Ophelia - Taylor Swift.mp3";
        _cli.FileExists(expectedPath).ShouldBeTrue();

        // Use ManageSongDevicesFlow to mark the device for removal via the UI
        await new ManageSongDevicesFlow("The Fate of Ophelia", _cli.DeviceName, "Remove").ExecuteAsync(Page);

        // Run sync again - should remove the local file
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();

        // Verify local file was removed
        _cli.FileExists(expectedPath).ShouldBeFalse();
    }
}