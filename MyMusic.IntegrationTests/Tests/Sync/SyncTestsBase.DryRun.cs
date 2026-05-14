using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
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

        // Run sync in dry-run mode
        var result = await App.SyncAsync(new SyncOptions { DryRun = true });
        result.ShouldBeSuccessful();

        // Dry run should report creation
        result.Created.ShouldBeGreaterThanOrEqualTo(1);

        // Song should NOT appear on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(0);

        // A subsequent real sync should upload the song successfully
        var realResult = await App.SyncAsync(new SyncOptions());
        realResult.ShouldBeSuccessful();
        realResult.Created.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Sync_DryRun_ShouldReportDownloadWithoutDownloading()
    {
        // Seed a song on the server assigned to this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] }]);

        // Run sync in dry-run mode
        var result = await App.SyncAsync(new SyncOptions { DryRun = true });
        result.ShouldBeSuccessful();

        // Dry run should report download
        result.Downloaded.ShouldBeGreaterThanOrEqualTo(1);

        // File should NOT exist locally (dry run doesn't download)
        var expectedPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        App.FileExists(expectedPath).ShouldBeFalse();

        // A subsequent real sync should download the file successfully
        var realResult = await App.SyncAsync(new SyncOptions());
        realResult.ShouldBeSuccessful();
        realResult.Downloaded.ShouldBeGreaterThanOrEqualTo(1);
        App.FileExists(expectedPath).ShouldBeTrue();
    }
}
