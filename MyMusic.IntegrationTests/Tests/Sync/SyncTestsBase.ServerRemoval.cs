using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_ShouldDeleteLocalFileWhenServerMarksForRemoval()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song to the device
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify file exists locally
        var expectedPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(expectedPath).ShouldBeTrue();

        // Mark device for removal on the server
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[0].Id, App.DeviceId);

        // Run sync again - should delete the local file
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(unlink: 1);

        // Verify local file was removed
        App.FileExists(expectedPath).ShouldBeFalse();

        // Verify the song-device association was removed
        await new ShouldSongExistInDeviceFlow("Wicker Woman", App.DeviceName, shouldExist: false)
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task Sync_ShouldRemoveLocalFileWhenServerSongDeleted()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[3] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song to the device
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify file exists locally
        var expectedPath = "Taylor Swift/The Life of a Showgirl/The Fate of Ophelia - Taylor Swift.mp3";
        App.FileExists(expectedPath).ShouldBeTrue();

        // Use ManageSongDevicesFlow to mark the device for removal via the UI
        await new ManageSongDevicesFlow("The Fate of Ophelia", App.DeviceName, "Remove").ExecuteAsync(Page);

        // Run sync again - should remove the local file
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(unlink: 1);

        // Verify local file was removed
        App.FileExists(expectedPath).ShouldBeFalse();
    }
}
