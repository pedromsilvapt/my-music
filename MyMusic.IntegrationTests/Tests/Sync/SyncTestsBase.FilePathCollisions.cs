using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: The same file in two folders becomes a single song that is on the device twice
    //   Given two identical copies of a song file in different folders on the device
    //   When the device syncs
    //   Then one song is created on the server and the other copy is linked to it
    //   And the server lists a single song
    //   And the song shows the device once per copy
    //   And the song's server file has no collision counter in its name
    [Fact]
    public async Task Sync_SameFileInTwoFolders_ShouldCreateOneSongWithTwoDevicePaths()
    {
        var song = new SampleSong("CollisionTest", "CollisionAlbum", ["CollisionArtist"], [], 2025);

        // Create identical files in two different sub-folders (same bytes = same checksum)
        await App.CreateSongAsync(song, "folder1/CollisionTest.mp3");
        await App.CreateSongAsync(song, "folder2/CollisionTest.mp3");

        // Sync should deduplicate by checksum: 1 song created, 1 linked
        var result = await App.SyncAsync(new SyncOptions());
        // First file creates the song on the server
        result.ShouldBe(createRemote: 1, link: 1);

        // Verify only one song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // Verify the song detail page shows 2 device badges (one per folder/file)
        var songDetails = await songs.Collection.GoToSongDetailsAsync(0);
        var deviceBadges = await songDetails.GetAllDeviceBadgesAsync();
        deviceBadges.Count.ShouldBe(2, "Song should have 2 device badges (one per folder)");

        // Verify repository path has no collision counter (only one song)
        var repositoryPath = await songDetails.GetRepositoryPathAsync();
        repositoryPath.ShouldBe($"{ServerRepositoryBase}/CollisionArtist/CollisionAlbum/CollisionTest - CollisionArtist.mp3");
    }

    // Scenario: Copying an already synced file to another folder adds a second copy to the same song
    //   Given a song file on the device
    //   When the device syncs
    //   Then the song is created on the server
    //   When the file is copied to a second folder on the device
    //   And the device syncs
    //   Then the copy is linked to the existing song, and the original file is left untouched
    //   And the server still lists a single song
    //   And the song shows the device once per copy
    //   And the song's server file has no collision counter in its name
    [Fact]
    public async Task Sync_SameFileCopiedAfterFirstSync_ShouldAddSecondDevicePath()
    {
        var song = new SampleSong("CollisionIncremental", "CollisionAlbum", ["CollisionArtist"], [], 2025);

        // First sync with one file: 1 song, 1 device path
        await App.CreateSongAsync(song, "folder1/CollisionIncremental.mp3");
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Copy the same file to a second folder (same bytes = same checksum)
        await App.CreateSongAsync(song, "folder2/CollisionIncremental.mp3");

        // Second sync: song already exists on server with same checksum, so link instead of create
        var result2 = await App.SyncAsync(new SyncOptions());
        // Second file should be linked to existing song (same checksum)
        result2.ShouldBe(link: 1, skipped: 1);

        // Verify only one song on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // Verify the song detail page shows 2 device badges
        var songDetails = await songs.Collection.GoToSongDetailsByTitleAsync("CollisionIncremental");
        var deviceBadges = await songDetails.GetAllDeviceBadgesAsync();
        deviceBadges.Count.ShouldBe(2, "Song should have 2 device badges after second sync");

        // Verify repository path unchanged (no collision counter needed)
        var repositoryPath = await songDetails.GetRepositoryPathAsync();
        repositoryPath.ShouldBe($"{ServerRepositoryBase}/CollisionArtist/CollisionAlbum/CollisionIncremental - CollisionArtist.mp3");
    }

    // Scenario: Identical files with different names in three folders become a single song
    //   Given three identical copies of a song file on the device, each in its own folder with its own file name
    //   When the device syncs
    //   Then one song is created on the server and the other two copies are linked to it
    //   And the server lists a single song
    //   And the song shows the device once per copy
    //   When the device syncs again
    //   Then all three files are found up to date
    [Fact]
    public async Task Sync_SameFileInThreeFoldersWithDifferentNames_ShouldCreateOneRemoteAndTwoLinks()
    {
        var song = new SampleSong("TripleDup", "TripleAlbum", ["TripleArtist"], [], 2025);

        // Create identical files in three different sub-folders with different file names (same checksum)
        await App.CreateSongAsync(song, "folder1/TripleDup_v1.mp3");
        await App.CreateSongAsync(song, "folder2/TripleDup_v2.mp3");
        await App.CreateSongAsync(song, "folder3/TripleDup_v3.mp3");

        // Sync should deduplicate by checksum: 1 CreateRemote, 2 Link
        var result = await App.SyncAsync(new SyncOptions());
        // First file creates the song on the server
        result.ShouldBe(createRemote: 1, link: 2);

        // Verify only one song exists on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1);

        // Verify the song detail page shows 3 device badges (one per path)
        var songDetails = await songs.Collection.GoToSongDetailsAsync(0);
        var deviceBadges = await songDetails.GetAllDeviceBadgesAsync();
        deviceBadges.Count.ShouldBe(3, "Song should have 3 device badges (one per folder/name)");

        // Idempotency: second sync should skip all 3 unchanged files
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 3);
    }

    // Scenario: Different files with the same tags become separate songs, with numbered server files
    //   Given a song file on the device
    //   When the device syncs
    //   Then the song is created on the server, and its server file has no collision counter in its name
    //   When a second file with the same tags but different content is added to the device
    //   And the device syncs
    //   Then a second song is created on the server, and the first file is left untouched
    //   And the second song's server file name ends with the counter "(2)"
    //   When a third file with the same tags but yet different content is added to the device
    //   And the device syncs
    //   Then a third song is created on the server, and the first two files are left untouched
    //   And the third song's server file name ends with the counter "(3)"
    [Fact]
    public async Task Sync_DifferentFilesSameMetadata_ShouldCreateDistinctSongsWithCollisionResolution()
    {
        var song = new SampleSong("CollisionMulti", "CollisionAlbum", ["CollisionArtist"], [], 2025);

        // First file with same metadata but different content (different checksum due to different lyrics)
        await App.CreateSongAsync(song, "CollisionMulti_v1.mp3", contentVariant: 1);

        // Sync: 1 song created, no collision yet
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 1);

        // Verify repository path is the base path (no collision counter)
        await new ValidateSongDetailsFlow("CollisionMulti", new ValidateSongOptions(
            RepositoryPath: $"{ServerRepositoryBase}/CollisionArtist/CollisionAlbum/CollisionMulti - CollisionArtist.mp3"))
            .ExecuteAsync(Page);

        // Second file with same metadata but different content (different checksum)
        await App.CreateSongAsync(song, "CollisionMulti_v2.mp3", contentVariant: 2);

        // Sync: 2nd song created with collision resolution
        var result2 = await App.SyncAsync(new SyncOptions());
        // Second song should be created; first file is skipped as unchanged
        result2.ShouldBe(createRemote: 1, skipped: 1);

        // Verify 2 songs on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);

        // Navigate to second song (the collision-resolved one) and verify its repository path
        var songDetails2 = await songs.Collection.GoToSongDetailsAsync(1);
        var repoPath2 = await songDetails2.GetRepositoryPathAsync();
        repoPath2.ShouldBe($"{ServerRepositoryBase}/CollisionArtist/CollisionAlbum/CollisionMulti - CollisionArtist (2).mp3");

        // Third file with same metadata but yet different content
        await App.CreateSongAsync(song, "CollisionMulti_v3.mp3", contentVariant: 3);

        // Sync: 3rd song created with collision counter (3)
        var result3 = await App.SyncAsync(new SyncOptions());
        // Third song should be created; first two files are skipped as unchanged
        result3.ShouldBe(createRemote: 1, skipped: 2);

        // Verify 3 songs on the server
        songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(3);

        // Navigate to third song and verify its repository path
        var songDetails3 = await songs.Collection.GoToSongDetailsAsync(2);
        var repoPath3 = await songDetails3.GetRepositoryPathAsync();
        repoPath3.ShouldBe($"{ServerRepositoryBase}/CollisionArtist/CollisionAlbum/CollisionMulti - CollisionArtist (3).mp3");
    }

    // Scenario: Server songs whose device paths collide are downloaded to the paths the server assigned them
    //   Given two server songs with the same tags but different content, both included on the device
    //   And the second one was therefore assigned a device path with a collision counter
    //   When the device syncs
    //   Then each song is downloaded to its assigned device path
    //   And a second sync finds the device up to date
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_ServerSongsWithCollidingDevicePaths_ShouldDownloadToAssignedPathsAndStayInSync(bool deduplicate)
    {
        var song = new SampleSong("CollisionDownload", "CollisionAlbum", ["CollisionArtist"], [], 2025, DeviceIds: [App.DeviceId]);

        // Seed two distinct server songs that generate the same device path, one after the other, so the
        // second one should be assigned the " (2)" variant of the path
        var first = await ServerSongs.SeedAsync(RequestContext, UserId, song with { Lyrics = "Variant 1" });
        var second = await ServerSongs.SeedAsync(RequestContext, UserId, song with { Lyrics = "Variant 2" });
        var firstPath = first.DevicePaths![App.DeviceId];
        var secondPath = second.DevicePaths![App.DeviceId];
        secondPath.ShouldEndWith(" (2).mp3");

        // Sync should download both songs, each to the device path the server holds for it
        var result = await App.SyncAsync(new SyncOptions { Deduplicate = deduplicate });
        result.ShouldBe(createLocal: 2);
        App.GetAllFiles().Count.ShouldBe(2);
        App.FilesShouldExist([firstPath, secondPath]);

        // A second sync should find both files where the server expects them, with nothing left to do
        var result2 = await App.SyncAsync(new SyncOptions { Deduplicate = deduplicate });
        result2.ShouldBe(skipped: 2);
    }
}
