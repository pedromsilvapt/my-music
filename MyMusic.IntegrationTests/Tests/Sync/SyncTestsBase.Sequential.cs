using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: Each title change made on the server reaches the device on the following sync
    //   Given a song on the server was downloaded to the device
    //   When the song's title is changed on the server
    //   And the CLI sync runs
    //   Then the local file is updated and renamed after the new title
    //   When the song's title is changed on the server again
    //   And the CLI sync runs
    //   Then the local file is updated and renamed after the latest title
    [Fact]
    public async Task Sync_ShouldHandleSuccessiveTitleChangesAcrossSyncs()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);

        // First sync - download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify original file exists
        var originalPath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        App.FileExists(originalPath).ShouldBeTrue();

        // Change title to "A" on server
        await new EditSongFlow("Sand", new(Title: "Title A")).ExecuteAsync(Page);

        // Second sync - should update and rename file
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Verify file was renamed to "Title A"
        var pathA = "Dove Cameron/Sand/Title A - Dove Cameron.mp3";
        App.FileExists(originalPath).ShouldBeFalse();
        App.FileExists(pathA).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(App.GetSongPath(pathA), title: "Title A");

        // Change title to "B" on server
        await new EditSongFlow("Title A", new(Title: "Title B")).ExecuteAsync(Page);

        // Third sync - should update and rename file again
        var result3 = await App.SyncAsync(new SyncOptions());
        result3.ShouldBe(updateLocal: 1, rename: 1);

        // Verify final state reflects title "B"
        var pathB = "Dove Cameron/Sand/Title B - Dove Cameron.mp3";
        App.FileExists(pathA).ShouldBeFalse();
        App.FileExists(pathB).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(App.GetSongPath(pathB), title: "Title B");
    }

    // Scenario: A song uploaded from the device keeps syncing in both directions afterwards
    //   Given a new song exists only on the device
    //   When the CLI sync runs
    //   Then the song is uploaded to the server
    //   When the local file's title is changed
    //   And the CLI sync runs
    //   Then the change is uploaded to the server
    //   When the song's title is changed on the server
    //   And the CLI sync runs
    //   Then the local file is updated and renamed after the server's title
    [Fact]
    public async Task Sync_ShouldUploadAndUpdateAndThenDownloadInSuccessiveSyncs()
    {
        // Create a local song
        await App.CreateSongAsync(SongsFixture.DefaultSongs[1]);

        // First sync - upload the new song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Modify the file locally
        var localPath = "The Alibi.mp3";
        await App.UpdateLocalFileMetadataAsync(localPath, new(Title: "Updated Alibi"));

        // Second sync - upload the local change
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateRemote: 1);

        // Modify the song on the server
        await new EditSongFlow("Updated Alibi", new(Title: "Server Updated Alibi")).ExecuteAsync(Page);

        // Third sync - download the server change
        var result3 = await App.SyncAsync(new SyncOptions());
        result3.ShouldBe(updateLocal: 1, rename: 1);

        // Verify final state reflects the server-side change
        var finalPath = "Dylan/The Alibi/Server Updated Alibi - Dylan.mp3";
        App.FileExists(finalPath).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(App.GetSongPath(finalPath), title: "Server Updated Alibi");
    }
}
