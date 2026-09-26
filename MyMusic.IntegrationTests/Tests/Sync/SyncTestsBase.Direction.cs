using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
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
