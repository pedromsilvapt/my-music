using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public class CliSyncTests : IntegrationTestBase
{
    private CliTestFixture _cli = null!;
    private SongsFixture _serverSongs = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _cli = new CliTestFixture();
        await _cli.InitializeAsync(RequestContext, UserId, UserName);
        _serverSongs = new SongsFixture();
    }

    public override async Task DisposeAsync()
    {
        await _cli.DisposeAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public async Task Sync_ShouldUploadAndDownloadChanges()
    {
        // Create a song on the CLI environment
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[1]);

        // Run CLI sync to upload the song to the server
        var result = await CliRunner.SyncAsync(_cli);
        result.Success.ShouldBeTrue($"CLI failed (exit={result.ExitCode})\nStdOut: {result.StandardOutput}\nStdErr: {result.StandardError}");

        // Verify the song appears on the server UI
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // Modify the song title on the server
        await new EditSongFlow("The Alibi", new(Title: "Updated Title")).ExecuteAsync(Page);

        // Run CLI sync again to download the server changes
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Validate the local file has the updated title
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath("Dylan/The Alibi/Updated Title - Dylan.mp3"), title: "Updated Title");
    }

    [Fact]
    public async Task Sync_ShouldMergeDeviceAndServerSongs()
    {
        // Create a local song on the device
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Seed a song on the server associated with this device
        await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[0] with { DeviceIds = [_cli.DeviceId] }]);

        // Run CLI sync to merge device and server songs
        var result = await CliRunner.SyncAsync(_cli);
        result.Success.ShouldBeTrue($"CLI failed (exit={result.ExitCode})\nStdOut: {result.StandardOutput}\nStdErr: {result.StandardError}");

        // Verify the device song still exists locally
        _cli.FileExists("Sand.mp3").ShouldBeTrue();

        // Verify the server song was downloaded to the device
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "U2/Days Of Ash EP/Yours Eternally (feat. Ed Sheeran & Taras Topolia) - U2.mp3";
        _cli.FileExists(expectedDevicePath).ShouldBeTrue();

        // Verify both songs exist on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Sync_ShouldUploadDeviceSongAndIdempotentOnSecondSync()
    {
        // Create a local song on the device
        await _cli.CreateSongAsync(SongsFixture.DefaultSongs[11]);

        // Run CLI sync to upload the song to the server
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.Success.ShouldBeTrue($"CLI failed (exit={result1.ExitCode})\nStdOut: {result1.StandardOutput}\nStdErr: {result1.StandardError}");

        // Verify the file still exists locally after upload
        _cli.FileExists("Girl across the street.mp3").ShouldBeTrue();

        // Verify the song exists on the server for this device
        await new ShouldSongExistInDeviceFlow("Girl across the street", _cli.DeviceName, shouldExist: true)
            .ExecuteAsync(Page);

        // Run CLI sync again (idempotency check)
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Verify no changes were made on the second sync
        result2.TotalChanges.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_ShouldDownloadServerSongAndIdempotentOnSecondSync()
    {
        // Seed a song on the server associated with this device
        var songsData = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[4] with { DeviceIds = [_cli.DeviceId] }]);

        // Run CLI sync to download the song to the device
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.Success.ShouldBeTrue($"CLI failed (exit={result1.ExitCode})\nStdOut: {result1.StandardOutput}\nStdErr: {result1.StandardError}");

        // Verify the file was downloaded with correct metadata
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "Dylan/Girl Of Your Dreams/Girl Of Your Dreams - Dylan.mp3";
        _cli.FileExists(expectedDevicePath).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath(expectedDevicePath), title: "Girl Of Your Dreams");

        // Run CLI sync again (idempotency check)
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Verify no changes were made on the second sync
        result2.TotalChanges.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_ShouldRemoveDeviceAssociationWhenSongRemovedFromDevice()
    {
        // Seed a song on the server associated with this device
        var songsData = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [_cli.DeviceId] }]);

        // Run CLI sync to download the song to the device
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.Success.ShouldBeTrue($"CLI failed (exit={result1.ExitCode})\nStdOut: {result1.StandardOutput}\nStdErr: {result1.StandardError}");

        // Verify the file exists locally
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        _cli.FileExists(expectedDevicePath).ShouldBeTrue();

        // Delete the local file to simulate user removing it
        File.Delete(_cli.GetSongPath(expectedDevicePath));

        // Run CLI sync to process the removal
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Verify the song still exists on the server (device association removed)
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Sync_ShouldUploadLocalChangesToServer()
    {
        var updatedTitle = "Updated Title";

        // Seed the song on the server
        var songsData = await _serverSongs.SeedAsync(RequestContext, UserId,
            SongsFixture.DefaultSongs.Take(1).Select(s => s with { DeviceIds = [_cli.DeviceId] }).ToArray());

        // Run the CLI sync command, should download the file to the local CLI folder
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.Success.ShouldBeTrue($"CLI failed (exit={result1.ExitCode})\nStdOut: {result1.StandardOutput}\nStdErr: {result1.StandardError}");

        // Validate the file was correctly downloaded
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "U2/Days Of Ash EP/Yours Eternally (feat. Ed Sheeran & Taras Topolia) - U2.mp3";
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath(expectedDevicePath), title: "Yours Eternally (feat. Ed Sheeran & Taras Topolia)");

        // Validate the song exists on the server with no sync action (flag removed after sync)
        await new ShouldSongExistInDeviceFlow("Yours Eternally (feat. Ed Sheeran & Taras Topolia)", _cli.DeviceName, shouldExist: true, shouldHaveNoSyncAction: true)
            .ExecuteAsync(Page);

        // Simulate manually changing the file locally on the device
        await _cli.UpdateLocalFileMetadataAsync(expectedDevicePath, new EditSongOptions(Title: updatedTitle));

        // Run the CLI sync command again, should upload the song
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Validate that the title of the song on the details page is updated now
        await new ValidateSongDetailsFlow(updatedTitle, new ValidateSongOptions(Title: updatedTitle))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task Sync_ShouldDownloadSongWhenAddedToDeviceAfterServerCreation()
    {
        // Seed song on server WITHOUT device association
        var songsData = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1]]);
        var songData = songsData[0];

        // Use ManageSongDevicesFlow to add song to device
        await new ManageSongDevicesFlow(songData.Title, _cli.DeviceName, "Add").ExecuteAsync(Page);

        // Run CLI sync to download the song to the device
        var result = await CliRunner.SyncAsync(_cli);
        result.Success.ShouldBeTrue($"CLI failed (exit={result.ExitCode})\nStdOut: {result.StandardOutput}\nStdErr: {result.StandardError}");
        result.Downloaded.ShouldBe(1);

        // Verify file exists locally with correct metadata
        // Expected path based on naming template: {artist[0]}/{album}/{simple_label}.mp3
        // simple_label = "{title} - {artists}" for songs without album artist
        var fixtureSong = SongsFixture.DefaultSongs[1];
        var expectedPath = $"{fixtureSong.Artists[0]}/{fixtureSong.Album}/{fixtureSong.Title} - {string.Join(", ", fixtureSong.Artists)}.mp3";
        await FileValidator.AssertMetadataAsync(
            _cli.GetSongPath(expectedPath),
            title: songData.Title);
    }

    [Fact]
    public async Task Sync_ShouldRenameFileWhenTitleChanges()
    {
        // Seed song on server WITH device association
        var songsData = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [_cli.DeviceId] }]);

        // Run CLI sync - verify file downloaded with original title
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.Success.ShouldBeTrue($"CLI failed (exit={result1.ExitCode})\nStdOut: {result1.StandardOutput}\nStdErr: {result1.StandardError}");

        var songData = songsData[0];
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var originalDevicePath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        _cli.FileExists(originalDevicePath).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath(originalDevicePath), title: songData.Title);

        // Edit song title via EditSongFlow
        var newTitle = "Updated Title";
        await new EditSongFlow(songData.Title, new(Title: newTitle)).ExecuteAsync(Page);

        // Run CLI sync again
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Verify: old file removed, new file exists with new title in filename
        var newDevicePath = "Freya Ridings/Wicker Woman/Updated Title - Freya Ridings.mp3";
        var newFileExists = _cli.FileExists(newDevicePath);
        var oldFileExists = _cli.FileExists(originalDevicePath);
        oldFileExists.ShouldBeFalse($"Old file should be removed.\nNew file exists: {newFileExists}\nCLI stdout: {result2.StandardOutput}\nCLI stderr: {result2.StandardError}");
        newFileExists.ShouldBeTrue("New file should exist");
        await FileValidator.AssertMetadataAsync(_cli.GetSongPath(newDevicePath), title: newTitle);
    }

    [Fact]
    public async Task Sync_ShouldRenameFileWhenExplicitFlagChanges()
    {
        // Seed song on server WITH device association (not explicit)
        var songsData = await _serverSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[3] with { DeviceIds = [_cli.DeviceId] }]);

        // Run CLI sync - verify file downloaded without "(Explicit)" in name
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.Success.ShouldBeTrue($"CLI failed (exit={result1.ExitCode})\nStdOut: {result1.StandardOutput}\nStdErr: {result1.StandardError}");

        var songData = songsData[0];
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3 (no Explicit suffix initially)
        var originalDevicePath = "Taylor Swift/The Life of a Showgirl/The Fate of Ophelia - Taylor Swift.mp3";
        _cli.FileExists(originalDevicePath).ShouldBeTrue();
        originalDevicePath.ShouldNotContain("(Explicit)");

        // Mark song as explicit via EditSongFlow
        await new EditSongFlow(songData.Title, new(Explicit: true)).ExecuteAsync(Page);

        // Run CLI sync again
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.Success.ShouldBeTrue($"CLI failed (exit={result2.ExitCode})\nStdOut: {result2.StandardOutput}\nStdErr: {result2.StandardError}");

        // Verify: old file removed, new file exists with "(Explicit)" in filename
        _cli.FileExists(originalDevicePath).ShouldBeFalse("Old file should be removed");
        // Expected path after marking explicit: {artist[0]}/{album}/{title} (Explicit) - {artists}.mp3
        var explicitPath = "Taylor Swift/The Life of a Showgirl/The Fate of Ophelia (Explicit) - Taylor Swift.mp3";
        _cli.FileExists(explicitPath).ShouldBeTrue("New file should exist with (Explicit) in filename");
    }

}
