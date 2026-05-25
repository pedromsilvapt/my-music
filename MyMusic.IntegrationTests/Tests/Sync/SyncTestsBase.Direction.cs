using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
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

        // Run sync with direction=up (upload only, no download)
        var result = await App.SyncAsync(new SyncOptions { Direction = SyncDirection.Up });
        result.ShouldBeSuccessful();

        // Local song should be uploaded (Created >= 1)
        result.CreateRemote.ShouldBe(1);

        // Server song should NOT be downloaded (CreateLocal should be 0)
        result.CreateLocal.ShouldBe(0);
        result.UpdateLocal.ShouldBe(0);

        // Verify the local song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBeGreaterThanOrEqualTo(2);
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
        result.ShouldBeSuccessful();

        // Server song should be downloaded (CreateLocal == 1)
        result.CreateLocal.ShouldBe(1);

        // Local song should NOT be uploaded (Created should be 0)
        result.CreateRemote.ShouldBe(0);
        result.UpdateRemote.ShouldBe(0);

        // Verify the downloaded file exists locally
        var expectedPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        App.FileExists(expectedPath).ShouldBeTrue();
    }
}
