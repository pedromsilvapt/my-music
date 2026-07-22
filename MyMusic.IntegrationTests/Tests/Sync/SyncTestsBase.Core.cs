using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_ShouldUploadAndDownloadChanges()
    {
        // Create a song on the CLI environment
        await App.CreateSongAsync(SongsFixture.DefaultSongs[1]);

        // Run CLI sync to upload the song to the server
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createRemote: 1);

        // Verify the song appears on the server UI
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // Modify the song title on the server
        await new EditSongFlow("The Alibi", new(Title: "Updated Title")).ExecuteAsync(Page);

        // Run CLI sync again to download the server changes
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Validate the local file has the updated title
        await FileValidator.AssertMetadataAsync(App.GetSongPath("Dylan/The Alibi/Updated Title - Dylan.mp3"), title: "Updated Title");
    }

    [Fact]
    public async Task Sync_ShouldMergeDeviceAndServerSongs()
    {
        // Create a local song on the device
        await App.CreateSongAsync(SongsFixture.DefaultSongs[5]);

        // Seed a song on the server associated with this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[0] with { DeviceIds = [App.DeviceId] }]);

        // Run CLI sync to merge device and server songs
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createRemote: 1, createLocal: 1);

        // Verify the device song still exists locally
        App.FileExists("Sand.mp3").ShouldBeTrue();

        // Verify the server song was downloaded to the device
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "U2/Days Of Ash EP/Yours Eternally (feat. Ed Sheeran & Taras Topolia) - U2.mp3";
        App.FileExists(expectedDevicePath).ShouldBeTrue();

        // Verify both songs exist on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Sync_ShouldUploadDeviceSongAndIdempotentOnSecondSync()
    {
        // Create a local song on the device
        await App.CreateSongAsync(SongsFixture.DefaultSongs[11]);

        // Run CLI sync to upload the song to the server
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Verify the file still exists locally after upload
        App.FileExists("Girl across the street.mp3").ShouldBeTrue();

        // Verify the song exists on the server for this device
        await new ShouldSongExistInDeviceFlow("Girl across the street", App.DeviceName, shouldExist: true)
            .ExecuteAsync(Page);

        // Run CLI sync again (idempotency check)
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 1);
    }

    [Fact]
    public async Task Sync_ShouldDownloadServerSongAndIdempotentOnSecondSync()
    {
        // Seed a song on the server associated with this device
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[4] with { DeviceIds = [App.DeviceId] }]);

        // Run CLI sync to download the song to the device
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify the file was downloaded with correct metadata
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "Dylan/Girl Of Your Dreams/Girl Of Your Dreams - Dylan.mp3";
        App.FileExists(expectedDevicePath).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(App.GetSongPath(expectedDevicePath), title: "Girl Of Your Dreams");

        // Run CLI sync again (idempotency check)
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 1);
    }

    [Fact]
    public async Task Sync_ShouldRemoveDeviceAssociationWhenSongRemovedFromDevice()
    {
        // Seed a song on the server associated with this device
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);

        // Run CLI sync to download the song to the device
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify the file exists locally
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        App.FileExists(expectedDevicePath).ShouldBeTrue();

        // Delete the local file to simulate user removing it
        File.Delete(App.GetSongPath(expectedDevicePath));

        // Run CLI sync to process the removal
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(unlink: 1);

        // Verify the song still exists on the server (device association removed)
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Sync_ShouldUploadLocalChangesToServer()
    {
        var updatedTitle = "Updated Title";

        // Seed the song on the server
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId,
            SongsFixture.DefaultSongs.Take(1).Select(s => s with { DeviceIds = [App.DeviceId] }).ToArray());

        // Run the CLI sync command, should download the file to the local CLI folder
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Validate the file was correctly downloaded
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var expectedDevicePath = "U2/Days Of Ash EP/Yours Eternally (feat. Ed Sheeran & Taras Topolia) - U2.mp3";
        await FileValidator.AssertMetadataAsync(App.GetSongPath(expectedDevicePath), title: "Yours Eternally (feat. Ed Sheeran & Taras Topolia)");

        // Validate the song exists on the server with no sync action (flag removed after sync)
        await new ShouldSongExistInDeviceFlow("Yours Eternally (feat. Ed Sheeran & Taras Topolia)", App.DeviceName, shouldExist: true, shouldHaveNoSyncAction: true)
            .ExecuteAsync(Page);

        // Simulate manually changing the file locally on the device
        await App.UpdateLocalFileMetadataAsync(expectedDevicePath, new EditSongOptions(Title: updatedTitle));

        // Run the CLI sync command again, should upload the song
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateRemote: 1);

        // Validate that the title of the song on the details page is updated now
        await new ValidateSongDetailsFlow(updatedTitle, new ValidateSongOptions(Title: updatedTitle))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task Sync_ShouldDownloadSongWhenAddedToDeviceAfterServerCreation()
    {
        // Seed song on server WITHOUT device association
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1]]);
        var songData = songsData[0];

        // Use ManageSongDevicesFlow to add song to device
        await new ManageSongDevicesFlow(songData.Title, App.DeviceName, "Add").ExecuteAsync(Page);

        // Run CLI sync to download the song to the device
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createLocal: 1);

        // Verify file exists locally with correct metadata
        // Expected path based on naming template: {artist[0]}/{album}/{simple_label}.mp3
        // simple_label = "{title} - {artists}" for songs without album artist
        var fixtureSong = SongsFixture.DefaultSongs[1];
        var expectedPath = $"{fixtureSong.Artists[0]}/{fixtureSong.Album}/{fixtureSong.Title} - {string.Join(", ", fixtureSong.Artists)}.mp3";
        await FileValidator.AssertMetadataAsync(
            App.GetSongPath(expectedPath),
            title: songData.Title);
    }

    [Fact]
    public async Task Sync_ShouldRenameFileWhenTitleChanges()
    {
        // Seed song on server WITH device association
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);

        // Run CLI sync - verify file downloaded with original title
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        var songData = songsData[0];
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3
        var originalDevicePath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(originalDevicePath).ShouldBeTrue();
        await FileValidator.AssertMetadataAsync(App.GetSongPath(originalDevicePath), title: songData.Title);

        // Edit song title via EditSongFlow
        var newTitle = "Updated Title";
        await new EditSongFlow(songData.Title, new(Title: newTitle)).ExecuteAsync(Page);

        // Run CLI sync again
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Verify: old file removed, new file exists with new title in filename
        var newDevicePath = "Freya Ridings/Wicker Woman/Updated Title - Freya Ridings.mp3";
        var newFileExists = App.FileExists(newDevicePath);
        var oldFileExists = App.FileExists(originalDevicePath);
        oldFileExists.ShouldBeFalse($"Old file should be removed.\nNew file exists: {newFileExists}");
        newFileExists.ShouldBeTrue("New file should exist");
        await FileValidator.AssertMetadataAsync(App.GetSongPath(newDevicePath), title: newTitle);
    }

    [Fact]
    public async Task Sync_ShouldRenameFileWhenExplicitFlagChanges()
    {
        // Seed song on server WITH device association (not explicit)
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[3] with { DeviceIds = [App.DeviceId] }]);

        // Run CLI sync - verify file downloaded without "(Explicit)" in name
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        var songData = songsData[0];
        // Expected path: {artist[0]}/{album}/{title} - {artists}.mp3 (no Explicit suffix initially)
        var originalDevicePath = "Taylor Swift/The Life of a Showgirl/The Fate of Ophelia - Taylor Swift.mp3";
        App.FileExists(originalDevicePath).ShouldBeTrue();
        originalDevicePath.ShouldNotContain("(Explicit)");

        // Mark song as explicit via EditSongFlow
        await new EditSongFlow(songData.Title, new(Explicit: true)).ExecuteAsync(Page);

        // Run CLI sync again
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Verify: old file removed, new file exists with "(Explicit)" in filename
        App.FileExists(originalDevicePath).ShouldBeFalse("Old file should be removed");
        // Expected path after marking explicit: {artist[0]}/{album}/{title} (Explicit) - {artists}.mp3
        var explicitPath = "Taylor Swift/The Life of a Showgirl/The Fate of Ophelia (Explicit) - Taylor Swift.mp3";
        App.FileExists(explicitPath).ShouldBeTrue("New file should exist with (Explicit) in filename");
    }

    [Fact]
    public async Task Sync_ShouldDeleteOriginalFileWhenMetadataChanged()
    {
        // Create the file locally
        var originalPath = await App.CreateSongAsync(SongsFixture.DefaultSongs[1]);

        // Perform an initial sync, to have the file on the server as well
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Sanity check, the local file remains on disk after the initial sync
        App.FileShouldExist(originalPath);

        // Edit the song title, means on next sync, it would be renamed
        await new EditSongFlow("Alibi", new(Title: "Alibi (Acoustic)")).ExecuteAsync(Page);

        // Now mark the song for removal from the device
        await new ManageSongDevicesFlow("Alibi (Acoustic)", App.DeviceName, "Remove").ExecuteAsync(Page);

        // Even though the song was marked for remove after the edit, it should still remove the original DevicePath
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(unlink: 1);

        // Verify the local file was actually deleted
        App.FileShouldNotExist(originalPath, "Unlink must target the existing DevicePath, not the new templated path");
    }
}
