using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: The song kept by a merge takes the file name of the song merged into it
    //   Given two songs on the device, each at the path the naming template gives the other one
    //   When the songs are merged on the server
    //   And the device checks its files one per request
    //   Then the merged song's file is deleted
    //   And the kept song's file is renamed to the path that deletion frees, without a collision counter
    //
    // The device reports its files in no particular order, and the path used to be free only when the
    // deleted file was reported first: keeping either song covers both orders.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Sync_MergedSongOnDevice_ShouldGiveItsFileNameToTheKeptSong(int keptIndex)
    {
        await App.SetNamingTemplateAsync("{{ album.name }}{{ extension }}");

        SampleSong[] songs =
        [
            new("Alpha Song", "Alpha Album", ["Alpha Artist"], [], 2025),
            new("Beta Song", "Beta Album", ["Beta Artist"], [], 2025),
        ];
        var kept = songs[keptIndex];
        var merged = songs[1 - keptIndex];

        // Setup: each song's file sits at the path the naming template gives the other song. The files are
        // created in the same order whichever song is kept, so the two cases report them in opposite orders
        string[] paths =
        [
            await App.CreateSongAsync(songs[0], $"{songs[1].Album}.mp3"),
            await App.CreateSongAsync(songs[1], $"{songs[0].Album}.mp3"),
        ];
        var keptPath = paths[keptIndex];
        var mergedPath = paths[1 - keptIndex];

        // Initial sync uploads both songs to the server
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 2);

        // Merge the songs on the server: the kept one gains the other's artist, so its file changes
        await new MergeSongsFlow(kept.Title!, merged.Title!).ExecuteAsync(Page);

        // Sync one file per check request, so the two files are never decided in the same request.
        // Should delete the merged song's file, and download the kept song's file renamed by the template
        await App.SetChunkSizeAsync(1);
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1, deleteLocal: 1);

        // The kept song should be the only file left, at the path the merged song's file had
        App.GetAllFiles().Count.ShouldBe(1);
        App.FileShouldExist(mergedPath, "The kept song should take the path freed by the merged song");
        App.FileShouldNotExist(keptPath);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(mergedPath), title: kept.Title);

        // A further sync should find the device up to date
        var result3 = await App.SyncAsync(new SyncOptions());
        result3.ShouldBe(skipped: 1);
    }
}
