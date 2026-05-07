using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public partial class CliSyncTests
{
    [Fact]
    public async Task Sync_WithDirectionUp_ShouldUploadWithoutDownloading()
    {
        // Create a local song
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Seed a different song on the server assigned to this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [_cli.DeviceId] }]);

        // Run sync with direction=up (upload only, no download)
        var result = await CliRunner.SyncAsync(_cli, direction: SyncDirection.Up);
        result.ShouldBeSuccessful();

        // Local song should be uploaded (Created >= 1)
        result.Created.ShouldBeGreaterThanOrEqualTo(1);

        // Server song should NOT be downloaded (Downloaded should be 0)
        result.Downloaded.ShouldBe(0);

        // Verify the local song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Sync_WithDirectionDown_ShouldDownloadWithoutUploading()
    {
        // Seed a song on the server assigned to this device
        var serverSongs = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [_cli.DeviceId] }]);

        // Create a local song that would normally be uploaded
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Run sync with direction=down (download only, no upload)
        var result = await CliRunner.SyncAsync(_cli, direction: SyncDirection.Down);
        result.ShouldBeSuccessful();

        // Server song should be downloaded (Downloaded >= 1)
        result.Downloaded.ShouldBeGreaterThanOrEqualTo(1);

        // Local song should NOT be uploaded (Created should be 0)
        result.Created.ShouldBe(0);

        // Verify the downloaded file exists locally
        var expectedPath = "Dylan/The Alibi/The Alibi - Dylan.mp3";
        _cli.FileExists(expectedPath).ShouldBeTrue();
    }
}