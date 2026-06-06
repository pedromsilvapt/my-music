using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
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
        result2.ShouldBe(unlink: 3);

        // Verify all local files were removed
        App.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeFalse();
        App.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeFalse();
        App.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeFalse();
    }
}
