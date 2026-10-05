using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: A local file matching an exclusion rule is left out of the sync
    //   Given two songs exist locally, one of them inside an excluded folder
    //   When the sync runs
    //   Then only the song outside the excluded folder is uploaded
    [Fact]
    public async Task Sync_ShouldNotUploadExcludedFiles()
    {
        // Exclude a folder, and create one song inside it and another outside
        await App.SetExcludePatternsAsync("Podcasts/");
        await App.CreateSongAsync(SongsFixture.DefaultSongs[1], "Podcasts/The Alibi.mp3");
        await App.CreateSongAsync(SongsFixture.DefaultSongs[2], "Wicker Woman.mp3");

        // Run sync - the excluded song should be neither uploaded nor counted
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createRemote: 1, skipped: 0, error: 0);

        // Only the song outside the excluded folder should be on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // The excluded file should be left untouched
        App.FileShouldExist("Podcasts/The Alibi.mp3");
    }

    // Scenario: The server asks for a download to a path matching an exclusion rule
    //   Given a server song is assigned to the device
    //   And its path on the device matches an exclusion rule
    //   When the sync runs (dry or real)
    //   Then the file is not downloaded and the session records an error for it
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_ShouldFailDownloadToExcludedPath(bool dryRun)
    {
        // Exclude the folder the server song would be downloaded to
        var excludedPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        await App.SetExcludePatternsAsync("Dylan/");

        // Seed a song on the server associated with this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] }]);

        // Run sync - the download the server planned should fail even though no file exists at that path
        var result = await App.SyncAsync(new SyncOptions { DryRun = dryRun });
        result.ShouldBe(successful: false, createLocal: 1, error: 1);

        // The file should not have been created
        App.FileShouldNotExist(excludedPath);

        // The session should report the excluded path as an error
        var errorPaths = await SessionRecordHelper.FetchRecordPathsAsync(RequestContext, App.DeviceId, result.SessionId!.Value, "Error");
        errorPaths.ShouldBe([excludedPath]);
    }

    // Scenario: A synced file starts matching an exclusion rule
    //   Given a local song was uploaded to the server
    //   When an exclusion rule matching it is added and the sync runs again
    //   Then the song is unlinked from the device, staying on the server and on disk
    [Fact]
    public async Task Sync_ShouldUnlinkSyncedFileThatBecomesExcluded()
    {
        // Create a song locally and upload it
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5], "Sand.mp3");
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Exclude the file - the sync no longer sends it, so the server should forget the device has it
        await App.SetExcludePatternsAsync("Sand.*");
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(unlink: 1, error: 0);

        // The file should stay on the device
        App.FileShouldExist("Sand.mp3");

        // The song should stay on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);
    }
}
