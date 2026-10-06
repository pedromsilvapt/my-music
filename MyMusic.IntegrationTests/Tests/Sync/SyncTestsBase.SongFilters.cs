using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;

namespace MyMusic.IntegrationTests.Tests.Sync;

/// <summary>
/// Filtering the songs of the library by how they are placed on a device.
/// </summary>
public abstract partial class SyncTestsBase
{
    // Scenario: Songs are filtered by where, and how many times, they are on a device
    //   Given a song synced from two identical files of the device
    //   And another song synced from a single file of the device
    //   When the user filters the songs by the folder of the second file
    //   Then only the song with two files is listed
    //   When the user filters the songs by having a single copy on the device
    //   Then only the other song is listed
    //   When the user filters the songs by having more than one copy on the device
    //   Then only the song with two files is listed
    [Fact]
    public async Task SongsFilter_ShouldMatchDevicePathAndCopies()
    {
        // Upload two identical local files and a different one: the identical ones become one song at two paths
        var copiedSong = SongsFixture.DefaultSongs[2];
        var singleSong = SongsFixture.DefaultSongs[0];
        await App.CreateSongsAsync(
            (copiedSong, "Music/Copied.mp3"),
            (copiedSong, "Local Copies/Copied.mp3"),
            (singleSong, "Music/Single.mp3"));
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createRemote: 2, link: 1);

        // Filtering by the folder only the second copy is in should list just the copied song
        var collection = await new FilterSongsFlow(@"device.path startsWith ""Local Copies/""").ExecuteAsync(Page);
        await Assertions.Expect(collection.GetRowByTitle(copiedSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(1);

        // Both songs are on the device: asking for a single copy there should list just the other song
        await collection.ApplyFilterAsync($@"device(name = ""{App.DeviceName}"" and copies = 1)");
        await Assertions.Expect(collection.GetRowByTitle(singleSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(1);

        // Only the copied song should be on the device more than once
        await collection.ApplyFilterAsync($@"device(name = ""{App.DeviceName}"" and copies > 1)");
        await Assertions.Expect(collection.GetRowByTitle(copiedSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(1);
    }
}
