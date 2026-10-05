using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: Deleting an album on the server updates the files of its songs on the device
    //   Given a song on the server was downloaded to the device
    //   When the song's album is deleted on the server and the CLI sync runs
    //   Then the local file is downloaded again, without its album
    //   And it moves to the "(No Album)" folder of its artist
    [Fact]
    public async Task Sync_ShouldDownloadSongAgainAfterItsAlbumIsDeleted()
    {
        // Seed a song on the server associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        var originalDevicePath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(originalDevicePath).ShouldBeTrue();

        // Delete the song's album on the server: the song should move to "(No Album)"
        await new DeleteAlbumFlow("Wicker Woman", fromDetailsPage: true).ExecuteAsync(Page);

        // Run CLI sync again: the rewritten file should be downloaded to its new path
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Verify: the old file is gone, and the new one no longer names the deleted album
        var newDevicePath = "Freya Ridings/(No Album)/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(originalDevicePath).ShouldBeFalse("Old file should be removed");
        App.FileExists(newDevicePath).ShouldBeTrue("New file should exist");
        await FileValidator.AssertMetadataAsync(App.GetSongPath(newDevicePath),
            title: "Wicker Woman", album: "(No Album)", artists: ["Freya Ridings"]);
    }

    // Scenario: Deleting an artist on the server updates the files of the songs it was featured in
    //   Given a song by two artists on the server was downloaded to the device
    //   When the featured artist is deleted on the server and the CLI sync runs
    //   Then the local file is downloaded again, with only its other artist
    //   And it is renamed to no longer mention the deleted artist
    [Fact]
    public async Task Sync_ShouldDownloadSongAgainAfterOneOfItsArtistsIsDeleted()
    {
        // Seed a song by two artists on the server associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[6] with { DeviceIds = [App.DeviceId] }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        var originalDevicePath = "Faithless/New Religion/New Religion - Faithless, Bebe Rexha.mp3";
        App.FileExists(originalDevicePath).ShouldBeTrue();

        // Delete the featured artist on the server: the song should be left with its other artist
        await new DeleteArtistFlow("Bebe Rexha").ExecuteAsync(Page);

        // Run CLI sync again: the rewritten file should be downloaded to its new path
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Verify: the old file is gone, and the new one no longer names the deleted artist
        var newDevicePath = "Faithless/New Religion/New Religion - Faithless.mp3";
        App.FileExists(originalDevicePath).ShouldBeFalse("Old file should be removed");
        App.FileExists(newDevicePath).ShouldBeTrue("New file should exist");
        await FileValidator.AssertMetadataAsync(App.GetSongPath(newDevicePath),
            title: "New Religion", album: "New Religion", artists: ["Faithless"]);
    }
}
