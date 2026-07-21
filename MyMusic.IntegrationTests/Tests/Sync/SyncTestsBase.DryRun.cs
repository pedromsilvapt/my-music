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

        // The counters should be the same with or without dry run
        var dryResult = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        dryResult.ShouldBe(link: 1, skipped: 2);
    }
}
