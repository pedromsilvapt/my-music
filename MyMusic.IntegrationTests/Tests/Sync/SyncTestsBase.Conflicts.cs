using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_ConflictResolution_ShouldAutoResolveWhenContentIdentical()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Modify the song title on the server
        await new EditSongFlow("Sand", new(Title: "Updated Sand")).ExecuteAsync(Page);

        // Modify the local file with the SAME title change
        // The local file path may change after sync, so check what exists
        var originalPath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        await App.UpdateLocalFileMetadataAsync(originalPath, new(Title: "Updated Sand"));

        // Run sync - conflict should be auto-resolved since content is identical
        var result2 = await App.SyncAsync(new SyncOptions());
        // Auto-resolved conflict: expected 0 or minimal conflicts
        result2.ShouldBe(updateTimestamp: 1);

        // Verify the final state is consistent (both sides should have "Updated Sand")
        // The file path should not have changed, because the conflict was auto-resolved:
        // both files were changed, but had the exact same checksum after the change
        await FileValidator.AssertMetadataAsync(
            App.GetSongPath("Dove Cameron/Sand/Sand - Dove Cameron.mp3"),
            title: "Updated Sand");
    }

    [Fact]
    public async Task Sync_ConflictResolution_ShouldReportTrueConflictWhenContentDiffers()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Modify the song title on the server to one title
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);

        // Modify the local file to a DIFFERENT title
        var originalPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        await App.UpdateLocalFileMetadataAsync(originalPath, new(Title: "Local Title"));

        // Run sync - should detect conflict since content differs
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(conflict: 1);
    }

    // Scenario: A conflict found in an earlier chunk is kept while later chunks download server changes
    //   Given two songs on the server were downloaded to the device
    //   When one song is edited differently on the server and on the device
    //   And the other song is edited only on the server
    //   And the device checks its files one per request
    //   Then the conflicting local file is kept
    //   And the other song's server version is downloaded in the same sync
    [Fact]
    public async Task Sync_ConflictResolution_ShouldKeepConflictAcrossChunks()
    {
        // Seed two songs on the server associated with this device, and download them
        await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] },
            SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] },
        ]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 2);

        // Edit the first song differently on each side, so it becomes a real conflict
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);
        var conflictPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        await App.UpdateLocalFileMetadataAsync(conflictPath, new(Title: "Local Title"));

        // Edit the second song only on the server, so the device should download it
        await new EditSongFlow("Sand", new(Title: "Updated Sand")).ExecuteAsync(Page);

        // Sync one file per check request: the conflict and the update are resolved in separate chunks
        await App.SetChunkSizeAsync(1);
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(conflict: 1, updateLocal: 1, rename: 1);

        // The conflicting local file should be untouched, and the other song should hold the server's version
        await FileValidator.AssertMetadataAsync(App.GetSongPath(conflictPath), title: "Local Title");
        var updatedPath = "Dove Cameron/Sand/Updated Sand - Dove Cameron.mp3";
        App.FileShouldExist(updatedPath);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(updatedPath), title: "Updated Sand");
    }

    // Scenario: A conflict at one path of a song does not hold back the song's other paths
    //   Given two identical local files were synced and linked to one song
    //   When one of the files is edited on the device
    //   And the song is edited on the server
    //   And the device checks its files one per request
    //   Then the edited file is kept as a conflict
    //   And the unchanged file receives the server's version in the same sync
    [Fact]
    public async Task Sync_ConflictResolution_ShouldUpdateOtherPathsOfConflictedSong()
    {
        // Upload two identical local files: they become one song, linked at both paths
        var unchangedPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        var conflictPath = "Local Copies/Wicker Woman.mp3";
        await App.CreateSongsAsync((SongsFixture.DefaultSongs[2], unchangedPath), (SongsFixture.DefaultSongs[2], conflictPath));
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1, link: 1);

        // Edit one of the copies on the device, and the song on the server with a different title
        await App.UpdateLocalFileMetadataAsync(conflictPath, new(Title: "Local Title"));
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);

        // Sync one file per check request: the commit should succeed with the conflict kept
        await App.SetChunkSizeAsync(1);
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(conflict: 1, updateLocal: 1, rename: 1);

        // The edited copy should be untouched, and the other path should hold the server's version
        await FileValidator.AssertMetadataAsync(App.GetSongPath(conflictPath), title: "Local Title");
        var updatedPath = "Freya Ridings/Wicker Woman/Server Title - Freya Ridings.mp3";
        App.FileShouldExist(updatedPath);
        App.FileShouldNotExist(unchangedPath);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(updatedPath), title: "Server Title");
    }

    // Scenario: A local copy of an older version of a song is not a real conflict
    //   Given a song on the server was downloaded to the device
    //   When the song is edited on the server
    //   And the local file is touched without changing its content
    //   Then the sync recognises the local file as a previous version of the song
    //   And downloads the server's version instead of reporting a conflict
    [Fact]
    public async Task Sync_ConflictResolution_ShouldDownloadServerVersionWhenLocalIsPreviousVersion()
    {
        // Seed song on server associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Edit the song on the server, so the device's file becomes a previous version of it
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);

        // Touch the local file: both sides look modified since the last sync, but the local
        // content is still exactly the version the server had before
        var originalPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        App.TouchLocalFile(originalPath);

        // Sync should resolve the conflict in favour of the server: download and rename the file
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // The device now holds the server's version at its new path
        var newPath = "Freya Ridings/Wicker Woman/Server Title - Freya Ridings.mp3";
        App.FileShouldExist(newPath);
        App.FileShouldNotExist(originalPath);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(newPath), title: "Server Title");
    }

    // Scenario: A new local file holding an older version of a server song is linked and updated
    //   Given a song exists on the server
    //   And the song was edited on the server
    //   When a local file holding the song's original content is synced
    //   Then the file is linked to the song instead of being imported
    //   And the server's version is downloaded over it in the same sync
    [Fact]
    public async Task Sync_ShouldLinkAndDownloadWhenNewLocalFileIsPreviousVersion()
    {
        // Seed the song on the server, then edit it so its original content becomes a previous version
        await ServerSongs.SeedAsync(RequestContext, UserId, [SongsFixture.DefaultSongs[2]]);
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);

        // Create a local file with the song's original content
        var localPath = await App.CreateSongAsync(SongsFixture.DefaultSongs[2]);

        // Sync should link the file to the song and download the server's version over it
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(link: 1, updateLocal: 1);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(localPath), title: "Server Title");

        // A second sync should find the device up to date
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 1);
    }

    // Scenario: A local file reverted to an older version of its song gets the server's version back
    //   Given a song was uploaded from the device
    //   And the song was edited on the server and downloaded to the device
    //   When the local file is changed back to the song's original content
    //   Then the sync does not upload the old content
    //   And downloads the server's version in the same sync
    [Fact]
    public async Task Sync_ShouldDownloadServerVersionWhenLocalChangeIsPreviousVersion()
    {
        // Upload the song from the device
        var originalPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        await App.CreateSongAsync(SongsFixture.DefaultSongs[2], originalPath);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Edit the song on the server and download the new version (renamed by the naming template)
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Change the local file back to the song's original content
        var newPath = "Freya Ridings/Wicker Woman/Server Title - Freya Ridings.mp3";
        await App.CreateSongAsync(SongsFixture.DefaultSongs[2], newPath);

        // Sync should keep the server's version: it is downloaded over the reverted file
        var result3 = await App.SyncAsync(new SyncOptions());
        result3.ShouldBe(updateLocal: 1);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(newPath), title: "Server Title");

        // A second sync should find the device up to date
        var result4 = await App.SyncAsync(new SyncOptions());
        result4.ShouldBe(skipped: 1);
    }
}
