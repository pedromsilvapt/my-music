using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_ConflictResolution_ShouldAutoResolveWhenContentIdentical()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[5] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Modify the song title on the server
        await new EditSongFlow("Sand", new(Title: "Updated Sand")).ExecuteAsync(Page);

        // Modify the local file with the SAME title change
        // The local file path may change after sync, so check what exists
        var originalPath = "Dove Cameron/Sand/Sand - Dove Cameron.mp3";
        await App.UpdateLocalFileMetadataAsync(originalPath, new(Title: "Updated Sand"));

        // Run sync - conflict should be auto-resolved since content is identical
        var result2 = await App.SyncAsync(new SyncOptions());
        // Auto-resolved conflict: expected 0 or minimal conflicts
        result2.ShouldBe(updateTimestamp: 1);

        // Verify the final state is consistent (both sides should have "Updated Sand")
        // The file path should not have changed, because the conflict was auto-resolved:
        // both files were changed, but had the exact same checksum after the change
        await FileValidator.AssertMetadataAsync(
            App.GetSongPath("Dove Cameron/Sand/Sand - Dove Cameron.mp3"),
            title: "Updated Sand");
    }

    [Fact]
    public async Task Sync_ConflictResolution_ShouldReportTrueConflictWhenContentDiffers()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Modify the song title on the server to one title
        await new EditSongFlow("Wicker Woman", new(Title: "Server Title")).ExecuteAsync(Page);

        // Modify the local file to a DIFFERENT title
        var originalPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        await App.UpdateLocalFileMetadataAsync(originalPath, new(Title: "Local Title"));

        // Run sync - should detect conflict since content differs
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(conflict: 1);
    }
}
