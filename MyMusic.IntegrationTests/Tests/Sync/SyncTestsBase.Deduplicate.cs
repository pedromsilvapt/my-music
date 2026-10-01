using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

/// <summary>
/// Soundalike deduplication (<c>--deduplicate</c>). Every test song has the same audio under different tags,
/// so any two of them are soundalikes: different checksums, same acoustic fingerprint.
/// </summary>
public abstract partial class SyncTestsBase
{
    private static readonly SampleSong ServerSong = SongsFixture.DefaultSongs[2];
    private static readonly SampleSong SoundalikeSong = SongsFixture.DefaultSongs[1];

    // Scenario: A new local file that sounds like a server song is linked to it and replaced by it
    //   Given a song exists on the server
    //   When a local file with the same audio but other tags is synced with deduplication
    //   Then the file is linked to the server song instead of being uploaded
    //   And the server's file is downloaded over it in the same sync
    [Fact]
    public async Task Sync_Deduplicate_ShouldLinkAndDownloadSoundalikeOfServerSong()
    {
        // Seed the song on the server, and create a local soundalike of it
        await ServerSongs.SeedAsync(RequestContext, UserId, [ServerSong]);
        var localPath = await App.CreateSongAsync(SoundalikeSong);

        // Sync should link the soundalike to the server song and download the server's file over it
        var result = await App.SyncAsync(new SyncOptions { Deduplicate = true });
        result.ShouldBe(link: 1, updateLocal: 1);
        await FileValidator.AssertMetadataAsync(App.GetSongPath(localPath), title: ServerSong.Title);

        // The library should still hold only the server song
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // A second sync should find the device up to date
        var result2 = await App.SyncAsync(new SyncOptions { Deduplicate = true });
        result2.ShouldBe(skipped: 1);
    }

    // Scenario: Soundalikes uploaded in the same session become a single song
    //   Given two new local files with the same audio but different tags
    //   When they are synced with deduplication
    //   Then one is uploaded and the other is linked to its song
    //   And the linked file is replaced by a copy of the uploaded one
    //   And a dry run records the same actions without changing anything
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_Deduplicate_ShouldUploadOnlyOneOfSoundalikesInTheSameSession(bool dryRun)
    {
        // Create two local soundalikes
        var firstPath = await App.CreateSongAsync(ServerSong);
        var secondPath = await App.CreateSongAsync(SoundalikeSong);

        // Sync should upload one file, and link the other one to its song and replace it with a copy of it
        var result = await App.SyncAsync(new SyncOptions { Deduplicate = true, DryRun = dryRun });
        result.ShouldBe(createRemote: 1, link: 1, updateLocal: 1);

        // A real run should leave one song on the server and both local files with the same content;
        // a dry run should change neither
        var firstTitle = (await FileValidator.GetMetadataAsync(App.GetSongPath(firstPath))).Title;
        var secondTitle = (await FileValidator.GetMetadataAsync(App.GetSongPath(secondPath))).Title;
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        if (dryRun)
        {
            (await songs.Collection.GetRowCountAsync()).ShouldBe(0);
            firstTitle.ShouldBe(ServerSong.Title);
            secondTitle.ShouldBe(SoundalikeSong.Title);
        }
        else
        {
            (await songs.Collection.GetRowCountAsync()).ShouldBe(1);
            secondTitle.ShouldBe(firstTitle);
        }

        // A following sync should repeat the same actions after a dry run, and find the device up to date
        // after a real one
        var result2 = await App.SyncAsync(new SyncOptions { Deduplicate = true });
        if (dryRun)
        {
            result2.ShouldBe(createRemote: 1, link: 1, updateLocal: 1);
        }
        else
        {
            result2.ShouldBe(skipped: 2);
        }
    }
}
