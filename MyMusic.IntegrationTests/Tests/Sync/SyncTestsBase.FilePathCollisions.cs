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
}
