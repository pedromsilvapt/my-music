using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_DryRun_ShouldReportCreationWithoutUploading()
    {
        // Create a local song that would be uploaded
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Capture device last sync date before dry-run
        var lastSyncBefore = await new GetDeviceLastSyncAtFlow(App.DeviceName).ExecuteAsync(Page);

        // Run sync in dry-run mode
        var result = await App.SyncAsync(new SyncOptions { DryRun = true });
        result.ShouldBe(createRemote: 1);

        // Dry run should not change device last sync date
        var lastSyncAfter = await new GetDeviceLastSyncAtFlow(App.DeviceName).ExecuteAsync(Page);
        lastSyncAfter.ShouldBe(lastSyncBefore, "Dry run should not change device last sync date");

        // Song should NOT appear on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(0);

        // A subsequent real sync should upload the song successfully
        var realResult = await App.SyncAsync(new SyncOptions());
        realResult.ShouldBe(createRemote: 1);

        // Go home first, to force a refresh when we check the songs again
        await new HomePage(Page).Navbar.GoToHomeAsync();

        // Song should appear on the server
        songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_UnimportableFileReportsError(bool dryRun)
    {
        // Create a valid local song, and a file with a music extension whose metadata cannot be read
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);
        await App.CreateUnreadableSongAsync("Corrupt/corrupt.mp3");

        // The unreadable file should be reported as an error at upload time in both modes,
        // instead of only failing the real commit; the valid song should still be created
        var result = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        result.ShouldBe(successful: false, createRemote: 1, error: 1);

        // The commit should go through: a real run imports the valid song, a dry run imports nothing
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(dryRun ? 0 : 1);
    }

    [Fact(Skip = "This test is wrong. Since EditSongFlow sets SyncAction=Download, it should still be set after a dry-run. We need to find a way to unset that flag before the dry-run, so we can make this validation.")]
    public async Task Sync_DryRun_ShouldNotPersistSyncActionWhenServerSongEdited()
    {
        // Create a local song and sync to upload it to the server
        await App.CreateSongAsync(SongsFixture.DefaultSongs[1]);
        var uploadResult = await App.SyncAsync(new SyncOptions());
        uploadResult.ShouldBeSuccessful();

        // Edit the song title on the server — this triggers MarkSongDevicesForDownloadAsync
        // which sets SyncAction = Download on the SongDevice
        await new EditSongFlow("The Alibi", new(Title: "The Alibi (Live)")).ExecuteAsync(Page);

        // Run sync in dry-run mode — CheckSync should NOT persist SyncAction to the database
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = true });
        dryResult.ShouldBeSuccessful();

        // BUG: SyncAction remains Download (set by SongUpdateService at edit time
        // and persisted by CheckSync even in dry-run), but since the download never
        // actually happened, SyncAction should be null
        await new ShouldSongExistInDeviceFlow("The Alibi (Live)", App.DeviceName,
            shouldExist: true, shouldHaveNoSyncAction: true).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Sync_DryRun_ShouldReportDownloadWithoutDownloading()
    {
        // Seed a song on the server assigned to this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] }]);

        // Run sync in dry-run mode
        var result = await App.SyncAsync(new SyncOptions { DryRun = true });
        result.ShouldBe(createLocal: 1);

        // File should NOT exist locally (dry run doesn't download)
        var expectedPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        App.FileExists(expectedPath).ShouldBeFalse();

        // A subsequent real sync should download the file successfully
        var realResult = await App.SyncAsync(new SyncOptions());
        realResult.ShouldBe(createLocal: 1);
        App.FileExists(expectedPath).ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_UpdatedDetectConflict(bool dryRun)
    {
        // Seed two songs on the server assigned to this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },  // The Alibi - Dylan
            SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] },  // Wicker Woman - Freya Ridings
        ]);

        // Initial sync o ensure both sides have same data
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 2);

        // Change file locally
        await App.UpdateLocalFileMetadataAsync(
            "Dylan/The Alibi/The Alibi - Dylan.mp3",
            new EditSongOptions(Title: "The Alibi (Edited)"));

        // Different edit on the server, means different checksum
        await new EditSongFlow("The Alibi", new(Title: "The Alibi (Unedited)")).ExecuteAsync(Page);

        // The counters should be the same with or without dry run
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        dryResult.ShouldBe(conflict: 1, skipped: 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_UpdatedResolveConflictWithDownload(bool dryRun)
    {
        // Seed a song on the server assigned to this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] }]);  // The Alibi - Dylan
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Edit the song differently on each side, so it becomes a real conflict
        var originalPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        await App.UpdateLocalFileMetadataAsync(originalPath, new EditSongOptions(Title: "The Alibi (Edited)"));
        await new EditSongFlow("The Alibi", new(Title: "The Alibi (Unedited)")).ExecuteAsync(Page);

        // The counters should be the same with or without dry run
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = dryRun, Conflicts = ConflictResolution.Download });
        dryResult.ShouldBe(updateLocal: 1, rename: 1);

        // Only a real run should replace the local file with the server's version
        if (dryRun)
        {
            await FileValidator.AssertMetadataAsync(App.GetSongPath(originalPath), title: "The Alibi (Edited)");

            // A dry run resolves nothing: the conflict should still be there for the next sync
            var result3 = await App.SyncAsync(new SyncOptions());
            result3.ShouldBe(conflict: 1);
        }
        else
        {
            var newPath = "Dylan/The Alibi/The Alibi (Unedited) - Dylan.mp3";
            await FileValidator.AssertMetadataAsync(App.GetSongPath(newPath), title: "The Alibi (Unedited)");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_UpdatedAutoResolveConflict(bool dryRun)
    {
        // Seed two songs on the server assigned to this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },  // The Alibi - Dylan
            SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] },  // Wicker Woman - Freya Ridings
        ]);

        // Initial sync o ensure both sides have same data
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 2);

        // Create new file locally
        await App.UpdateLocalFileMetadataAsync(
            "Dylan/The Alibi/The Alibi - Dylan.mp3",
            new EditSongOptions(Title: "The Alibi (Edited)"));

        // Same edit on the server, means same checksum, should auto-resolve
        await new EditSongFlow("The Alibi", new(Title: "The Alibi (Edited)")).ExecuteAsync(Page);

        // The counters should be the same with or without dry run
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        dryResult.ShouldBe(updateTimestamp: 1, skipped: 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_CreatedDetectConflict(bool dryRun)
    {
        // Seed two songs on the server assigned to this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },  // The Alibi - Dylan
            SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] },  // Wicker Woman - Freya Ridings
        ]);

        // Initial sync o ensure both sides have same data
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 2);

        // Create a new file locally on the App
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Create the same file on the server, but with a different Year (different checksum)
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { Year = 2024, DeviceIds = [App.DeviceId] }]);

        // The counters should be the same with or without dry run
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        dryResult.ShouldBe(createRemote: 1, createLocal: 1, skipped: 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_CreatedAutoResolveConflict(bool dryRun)
    {
        // Seed two songs on the server assigned to this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },  // The Alibi - Dylan
            SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] },  // Wicker Woman - Freya Ridings
        ]);

        // Initial sync o ensure both sides have same data
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 2);

        // Create a new file locally on the App
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Create the same file on the server, with the same metadata (same checksum, conflict resolved)
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);

        // The local file should be linked to the server song, and the song's pending download at its
        // template path unlinked, since the device already holds its content.
        // The counters should be the same with or without dry run
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        dryResult.ShouldBe(link: 1, unlink: 1, skipped: 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_RenamedLocalCopyOfPendingDownload_ShouldLinkAndUnlinkOldPath(bool dryRun)
    {
        // Seed a song on the server assigned to this device, and sync it down
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Rename the local file, so it no longer sits at the song's Device Path
        var originalPath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        var renamedPath = "Dove Cameron/Sand/Sand (Explicit) - Dove Cameron.mp3";
        App.MoveLocalFile(originalPath, renamedPath);

        // Edit the song on the server without changing its path, so it becomes pending download
        await new EditSongFlow("Sand", new(Year: 2020)).ExecuteAsync(Page);

        // Link and unlink signifies the file was manually moved, and the old file path no longer exists
        // Update Local is because the new file was matched to an older checksum of the song, not to the latest
        var result2 = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        result2.ShouldBe(link: 1, updateLocal: 1, unlink: 1);

        // The song should only exist at the renamed path
        App.FileShouldNotExist(originalPath);
        App.FileShouldExist(renamedPath);

        // Expect the year to be updated locally in a real run, to saty the same in a dry run
        var expectedYear = dryRun ? 2023 : 2020;
        await FileValidator.AssertMetadataAsync(App.GetSongPath(renamedPath), year: expectedYear);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_DryRun_RenameAndUploadInSameSync_ShouldNotUnlinkRenamedSong(bool dryRun)
    {
        // Seed a song on the server assigned to this device, and sync it down
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Change the title on the server, which changes the file's path under the naming template
        await new EditSongFlow("Sand", new(Title: "Title A")).ExecuteAsync(Page);

        // Create a new local song, so the same sync also imports a song on the server
        await App.CreateSongAsync(SongsFixture.DefaultSongs[1]);

        // Sync: the edited song should be updated and renamed, and the new song uploaded.
        // The renamed song must not be unlinked, and dry-run and real run should report the same.
        var result2 = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        result2.ShouldBe(updateLocal: 1, rename: 1, createRemote: 1, unlink: 0);

        // The real run should keep the renamed song on the device
        if (!dryRun)
        {
            await new ShouldSongExistInDeviceFlow("Title A", App.DeviceName, shouldExist: true)
                .ExecuteAsync(Page);
        }
    }
}
