using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

/// <summary>
/// Managing a song that is more than once on a device.
/// </summary>
public abstract partial class SyncTestsBase
{
    // Scenario: Each copy of a song on a device is shown, and can be removed on its own
    //   Given a song synced from two identical files of the device
    //   When the user opens the devices of the song
    //   Then both copies are listed under the device, each with its path
    //   When the user removes one of the copies from the device
    //   And the sync runs
    //   Then only the file of that copy is deleted from the device
    //   And the other copy is the only one listed under the device
    [Fact]
    public async Task ManageDevices_ShouldShowAndRemoveEachCopyOfASong()
    {
        // Upload two identical local files: they become one song at two paths of the device
        var song = SongsFixture.DefaultSongs[2];
        var keptPath = "Music/Copied.mp3";
        var removedPath = "Local Copies/Copied.mp3";
        await App.CreateSongsAsync((song, keptPath), (song, removedPath));
        var uploadResult = await App.SyncAsync(new SyncOptions());
        uploadResult.ShouldBe(createRemote: 1, link: 1);

        // Both copies should be listed under the device, each at its own path
        await new ValidateSongCopiesInDeviceFlow(song.Title!, App.DeviceName, [keptPath, removedPath]).ExecuteAsync(Page);

        // Remove only the second copy, then sync: only its file should be deleted, the other is left as is
        await new RemoveSongCopyFromDeviceFlow(song.Title!, App.DeviceName, removedPath).ExecuteAsync(Page);
        var removeResult = await App.SyncAsync(new SyncOptions());
        removeResult.ShouldBe(deleteLocal: 1, skipped: 1);
        App.FileExists(removedPath).ShouldBeFalse();
        App.FileExists(keptPath).ShouldBeTrue();

        // The song should still be on the device, at the remaining path only
        await new ValidateSongCopiesInDeviceFlow(song.Title!, App.DeviceName, [keptPath]).ExecuteAsync(Page);
    }
}
