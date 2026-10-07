using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: Several new local songs are uploaded in a single sync
    //   Given three songs exist on the device that are not on the server
    //   When the CLI sync runs
    //   Then the three songs are uploaded
    //   And the songs page lists the three songs
    [Fact]
    public async Task Sync_ShouldHandleMultipleSimultaneousUploads()
    {
        // Create 3 local songs
        await App.CreateSongAsync(SongsFixture.DefaultSongs[1]);
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);
        await App.CreateSongAsync(SongsFixture.DefaultSongs[6]);

        // Run a single sync
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createRemote: 3);

        // Verify all 3 songs exist on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(3);
    }

    // Scenario: A single sync both downloads and uploads songs
    //   Given a song exists on the server, added to the device but not yet downloaded
    //   And another song exists on the device that is not on the server
    //   When the CLI sync runs
    //   Then the server song is downloaded to the device
    //   And the local song is uploaded
    //   And the songs page lists both songs
    [Fact]
    public async Task Sync_ShouldHandleMixedOperationsInOneSync()
    {
        // Seed a song on server assigned to this device (will be downloaded)
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },
        ]);

        // Create a new local song (will be uploaded)
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Run a single sync
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createLocal: 1, createRemote: 1);

        // Verify the downloaded file exists
        App.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeTrue();

        // Verify the uploaded song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);
    }

    // Scenario: Several server songs are downloaded in a single sync
    //   Given three songs exist on the server, added to the device but not yet downloaded
    //   When the CLI sync runs
    //   Then the three songs are downloaded to the device
    [Fact]
    public async Task Sync_ShouldHandleMultipleServerDownloadsInOneSync()
    {
        // Seed 3 songs on the server all assigned to this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },
            SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] },
            SongsFixture.DefaultSongs[6] with { DeviceIds = [App.DeviceId] },
        ]);

        // Run a single sync
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createLocal: 3);

        // Verify all files exist locally
        App.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeTrue();
        App.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeTrue();
        App.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeTrue();
    }

    // Scenario: Several songs removed from the device on the server are deleted in a single sync
    //   Given three songs exist on the server, added to the device
    //   When the CLI sync runs
    //   Then the three songs are downloaded to the device
    //   When the three songs are marked for removal from the device on the server
    //   And the CLI sync runs
    //   Then the three files are deleted from the device
    [Fact]
    public async Task Sync_ShouldHandleMultipleServerRemovesInOneSync()
    {
        // Seed 3 songs on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId] },
            SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] },
            SongsFixture.DefaultSongs[6] with { DeviceIds = [App.DeviceId] },
        ]);

        // Run sync to download all songs
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 3);

        // Verify all files exist locally
        App.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeTrue();
        App.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeTrue();
        App.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeTrue();

        // Mark all 3 for removal on the server
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[0].Id, App.DeviceId);
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[1].Id, App.DeviceId);
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[2].Id, App.DeviceId);

        // Run sync again - should remove all local files
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(deleteLocal: 3);

        // Verify all local files were removed
        App.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeFalse();
        App.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeFalse();
        App.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeFalse();
    }
}
