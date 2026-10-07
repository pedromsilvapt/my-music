using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: An upload-only sync uploads local songs without downloading the server's ones
    //   Given a song exists only on the device
    //   And a different song exists on the server, assigned to the device
    //   When the sync runs in the upload-only direction
    //   Then the local song is uploaded
    //   And the server's song is not downloaded, being unlinked from the device instead
    //   And both songs are listed on the server
    [Fact]
    public async Task Sync_WithDirectionUp_ShouldUploadWithoutDownloading()
    {
        if (!App.SupportsSyncDirection())
        {
            return; // Application does not support sync direction filtering
        }

        // Create a local song
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Seed a different song on the server assigned to this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] }]);

        // Run sync with direction=up (upload only, no download, unlink the old song)
        var result = await App.SyncAsync(new SyncOptions { Direction = SyncDirection.Up });
        result.ShouldBe(createRemote: 1, createLocal: 0, updateLocal: 0, unlink: 1);

        // Verify the local song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);
    }

    // Scenario: An upload-only sync keeps a local file whose song is pending removal on the server
    //   Given a song assigned to the device was already downloaded by a sync
    //   And the song is marked for removal from the device on the server
    //   When the sync runs in the upload-only direction, either as a dry run or for real
    //   Then the unchanged file is reported as skipped, and no deletion is reported
    //   And the local file is kept
    //   And the real run clears the pending removal, while the dry run leaves it pending
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_WithDirectionUp_ShouldKeepLocalFileMarkedForRemovalOnServer(bool dryRun)
    {
        if (!App.SupportsSyncDirection())
        {
            return; // Application does not support sync direction filtering
        }

        // Seed a song on the server assigned to this device, and sync it down
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Mark the song for removal from this device on the server
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[0].Id, App.DeviceId);

        // Sync with direction=up: the device is the source of truth, so the pending removal is
        // ignored and the unchanged file is skipped. Dry-run and real run should report the same.
        var result2 = await App.SyncAsync(new SyncOptions { Direction = SyncDirection.Up, DryRun = dryRun });
        result2.ShouldBe(skipped: 1, deleteLocal: 0);

        // The local file should be kept in both modes
        App.FileExists("Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3").ShouldBeTrue();

        // A real `up` sync clears the pending removal; a dry run leaves it untouched
        await (dryRun
                ? new ShouldSongExistInDeviceFlow("Wicker Woman", App.DeviceName, shouldExist: false, syncAction: "Remove")
                : new ShouldSongExistInDeviceFlow("Wicker Woman", App.DeviceName, shouldExist: true, shouldHaveNoSyncAction: true))
            .ExecuteAsync(Page);
    }

    // Scenario: A download-only sync downloads the server's songs without uploading local ones
    //   Given a song exists on the server, assigned to the device
    //   And a different song exists only on the device
    //   When the sync runs in the download-only direction
    //   Then the server's song is downloaded
    //   And the local song is not uploaded
    //   And the downloaded file exists on the device
    [Fact]
    public async Task Sync_WithDirectionDown_ShouldDownloadWithoutUploading()
    {
        if (!App.SupportsSyncDirection())
        {
            return; // Application does not support sync direction filtering
        }

        // Seed a song on the server assigned to this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] }]);

        // Create a local song that would normally be uploaded
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Run sync with direction=down (download only, no upload)
        var result = await App.SyncAsync(new SyncOptions { Direction = SyncDirection.Down });
        result.ShouldBe(createLocal: 1, createRemote: 0, updateRemote: 0);

        // Verify the downloaded file exists locally
        var expectedPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        App.FileExists(expectedPath).ShouldBeTrue();
    }

    // Scenario: A download-only sync deletes a local file whose song is pending removal on the server
    //   Given a song assigned to the device was already downloaded by a sync
    //   And the song is marked for removal from the device on the server
    //   When the sync runs in the download-only direction, either as a dry run or for real
    //   Then the file is reported as deleted
    //   And the real run deletes the local file and removes the song from the device
    //   And the dry run keeps the local file and leaves the removal pending
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_WithDirectionDown_ShouldDeleteLocalFileMarkedForRemovalOnServer(bool dryRun)
    {
        if (!App.SupportsSyncDirection())
        {
            return; // Application does not support sync direction filtering
        }

        // Seed a song on the server assigned to this device, and sync it down
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Mark the song for removal from this device on the server
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[0].Id, App.DeviceId);

        // Sync with direction=down: the server is the source of truth, so the removal is applied.
        // Dry-run and real run should report the same.
        var result2 = await App.SyncAsync(new SyncOptions { Direction = SyncDirection.Down, DryRun = dryRun });
        result2.ShouldBe(deleteLocal: 1);

        // The local file should only be deleted in the real run. The device association should be
        // removed by the real run, and left untouched (still pending removal) by the dry run.
        App.FileExists("Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3").ShouldBe(dryRun);
        await new ShouldSongExistInDeviceFlow("Wicker Woman", App.DeviceName, shouldExist: false,
                syncAction: dryRun ? "Remove" : null)
            .ExecuteAsync(Page);
    }

    // Scenario: A download-only sync renames a local file when its song's title changes on the server
    //   Given a song assigned to the device was already downloaded by a sync
    //   And the song's title is changed on the server
    //   When the sync runs in the download-only direction, either as a dry run or for real
    //   Then the file is reported as updated and renamed
    //   And the real run moves the file to the path matching the new title
    //   And the dry run leaves the file at its original path
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_WithDirectionDown_ShouldRenameLocalFileWhenServerTitleChanges(bool dryRun)
    {
        if (!App.SupportsSyncDirection())
        {
            return; // Application does not support sync direction filtering
        }

        // Seed a song on the server assigned to this device, and sync it down
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Change the title on the server, which changes the file's path under the naming template
        await new EditSongFlow("Sand", new(Title: "Title A")).ExecuteAsync(Page);

        // Sync with direction=down: the file should be updated and renamed.
        // Dry-run and real run should report the same.
        var result2 = await App.SyncAsync(new SyncOptions { Direction = SyncDirection.Down, DryRun = dryRun });
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // The file should only move to the new path in the real run
        App.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBe(dryRun);
        App.FileExists("Dove Cameron/Sand/Title A - Dove Cameron.mp3").ShouldBe(!dryRun);
    }
}
