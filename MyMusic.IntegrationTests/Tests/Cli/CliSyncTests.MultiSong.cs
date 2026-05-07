using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public partial class CliSyncTests
{
    [Fact]
    public async Task Sync_ShouldHandleMultipleSimultaneousUploads()
    {
        // Create 3 local songs
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[1]);
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[5]);
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[6]);

        // Run a single sync
        var result = await CliRunner.SyncAsync(_cli);
        result.ShouldBeSuccessful();

        // All 3 songs should be created
        result.Created.ShouldBe(3);

        // Verify all 3 songs exist on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task Sync_ShouldHandleMixedOperationsInOneSync()
    {
        // Seed a song on server assigned to this device (will be downloaded)
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [_cli.DeviceId] },
        ]);

        // Create a new local song (will be uploaded)
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Run a single sync
        var result = await CliRunner.SyncAsync(_cli);
        result.ShouldBeSuccessful();

        // Should have both downloads and creations
        result.Downloaded.ShouldBeGreaterThanOrEqualTo(1);
        result.Created.ShouldBeGreaterThanOrEqualTo(1);

        // Verify the downloaded file exists
        _cli.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeTrue();

        // Verify the uploaded song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Sync_ShouldHandleMultipleServerDownloadsInOneSync()
    {
        // Seed 3 songs on the server all assigned to this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [_cli.DeviceId] },
            SongsFixture.DefaultSongs[5] with { DeviceIds = [_cli.DeviceId] },
            SongsFixture.DefaultSongs[6] with { DeviceIds = [_cli.DeviceId] },
        ]);

        // Run a single sync
        var result = await CliRunner.SyncAsync(_cli);
        result.ShouldBeSuccessful();

        // All 3 songs should be downloaded
        result.Downloaded.ShouldBe(3);

        // Verify all files exist locally
        _cli.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeTrue();
        _cli.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeTrue();
        _cli.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeTrue();
    }

    [Fact]
    public async Task Sync_ShouldHandleMultipleServerRemovesInOneSync()
    {
        // Seed 3 songs on server associated with this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
        [
            SongsFixture.DefaultSongs[1] with { DeviceIds = [_cli.DeviceId] },
            SongsFixture.DefaultSongs[5] with { DeviceIds = [_cli.DeviceId] },
            SongsFixture.DefaultSongs[6] with { DeviceIds = [_cli.DeviceId] },
        ]);

        // Run sync to download all songs
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();

        // Verify all files exist locally
        _cli.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeTrue();
        _cli.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeTrue();
        _cli.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeTrue();

        // Mark all 3 for removal on the server
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[0].Id, _cli.DeviceId);
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[1].Id, _cli.DeviceId);
        await SongsFixture.MarkSongForRemovalAsync(RequestContext, serverSongs[2].Id, _cli.DeviceId);

        // Run sync again - should remove all local files
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();

        // Verify all local files were removed
        _cli.FileExists("Dylan/The Alibi/The Alibi - Dylan.mp3").ShouldBeFalse();
        _cli.FileExists("Dove Cameron/Sand/Sand - Dove Cameron.mp3").ShouldBeFalse();
        _cli.FileExists("Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3").ShouldBeFalse();
    }
}