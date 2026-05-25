using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_WithCustomNamingTemplate_ShouldPlaceFileInCorrectFolder()
    {
        var namingTemplate = "{{ year }}/{{ title }} - {{ artists_label }}{{ extension }}";
        await App.SetNamingTemplateAsync(namingTemplate);

        // Create a song in an arbitrary folder — its path template variables should be derived from the file's location
        var songA = SongsFixture.DefaultSongs[1]; // The Alibi, year 2024
        var pathA = $"OriginalFolder/{songA.Title} - {songA.Artists![0]}.mp3";
        await App.CreateSongAsync(songA, pathA);
        App.FileExists(pathA).ShouldBeTrue();

        // Initial sync uploads the song to the server
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBeSuccessful();
        result1.CreateRemote.ShouldBe(1);

        // Editing the year should trigger a rename
        await new EditSongFlow(songA.Title!, new(Year: 2025)).ExecuteAsync(Page);

        // Second sync should download the renamed file to the new year-based path
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBeSuccessful();
        result2.UpdateLocal.ShouldBe(1);

        var expectedPath = $"2025/{songA.Title} - {songA.Artists[0]}.mp3";
        App.FileShouldExist(expectedPath);
    }

    [Fact]
    public async Task Sync_ShouldPreserveOriginalFolderOnMetadataChange()
    {
        var namingTemplate = "{{ original_folder ?? year }}/{{ title }} - {{ artists_label }}{{ extension }}";
        await App.SetNamingTemplateAsync(namingTemplate);

        // Prepare four songs in year-based subfolders — original_folder should be derived from these paths
        var songA = SongsFixture.DefaultSongs[1]; // Year will change — should stay in same folder
        var songB = SongsFixture.DefaultSongs[2]; // Title will change — should be renamed
        var songC = SongsFixture.DefaultSongs[5]; // No changes — control group
        var songD = SongsFixture.DefaultSongs[6]; // No changes — control group (multi-artist)
        var songE = SongsFixture.DefaultSongs[4]; // Seeded server-side — tests fallback path

        var createdPaths = await App.CreateSongsAsync(
            (songA, $"2024/{songA.Title} - {songA.Artists[0]}.mp3"),
            (songB, $"2025/{songB.Title} - {songB.Artists[0]}.mp3"),
            (songC, $"2023/{songC.Title} - {songC.Artists[0]}.mp3"),
            (songD, $"2026/{songD.Title} - {string.Join(", ", songD.Artists)}.mp3")
        );

        App.FilesShouldExist(createdPaths);

        // Initial sync should upload all four songs, preserving their original folder paths
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBeSuccessful();
        result1.CreateRemote.ShouldBe(4);

        App.FilesShouldExist(createdPaths);

        // Mutate songs: change A's year and B's title, then seed a new server-side song E
        var updatedTitle = "Updated Woman";
        await new EditSongFlow(songB.Title, new(Title: updatedTitle)).ExecuteAsync(Page);
        await new EditSongFlow(songA.Title, new(Year: 2025)).ExecuteAsync(Page);

        var newServerSong = songE with { DeviceIds = [App.DeviceId] };
        await ServerSongs.SeedAsync(RequestContext, UserId, [newServerSong]);

        // Second sync downloads all three changes: year-changed A, title-changed B, and new song E
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBeSuccessful();
        result2.UpdateLocal.ShouldBe(2, "Should update 2 songs: year-changed A, title-changed B");
        result2.CreateLocal.ShouldBe(1, "Should update 2 songs: new E");

        // Song A changed year but original_folder should be preserved — file stays in the same folder
        App.FileShouldExist(createdPaths[0], "Song A (year change) should stay in same folder");

        // Song B was renamed — old path should be gone, new path should appear
        var expectedPathB = $"2025/{updatedTitle} - {songB.Artists[0]}.mp3";
        App.FileShouldNotExist(createdPaths[1], "Old path for Song B should be removed");
        App.FileShouldExist(expectedPathB, "Song B (title change) should be renamed");

        // Songs C and D were not modified — they should remain unchanged
        App.FileShouldExist(createdPaths[2], "Song C should remain unchanged");
        App.FileShouldExist(createdPaths[3], "Song D should remain unchanged");

        // Song E was seeded server-side — it should be downloaded using the fallback (year from metadata)
        var expectedPathE = $"2022/{songE.Title} - {songE.Artists[0]}.mp3";
        App.FileShouldExist(expectedPathE, "New server song E should be downloaded with year-based path");
    }

    [Fact]
    public async Task Sync_ShouldPreserveFolderOnYearChange_SingleSong()
    {
        // Test: A single song in folder 2024 with year 2024 metadata
        // When year changes to 2025 on server, file should stay in 2024 folder
        // but local metadata should reflect the new year

        var namingTemplate = "{{ original_folder ?? year }}/{{ title }} - {{ artists_label }}{{ extension }}";
        await App.SetNamingTemplateAsync(namingTemplate);

        // Create a single song in 2024 folder with year 2024 metadata
        var songA = SongsFixture.DefaultSongs[1]; // The Alibi, year 2024
        var pathA = $"2024/{songA.Title} - {songA.Artists[0]}.mp3";
        var createdPathA = await App.CreateSongAsync(songA, pathA);

        App.FileExists(createdPathA).ShouldBeTrue($"Created path should exist: {createdPathA}");

        // Initial sync uploads the song to server, deriving original_folder from path
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBeSuccessful();
        result1.CreateRemote.ShouldBe(1);

        App.FileShouldExist(createdPathA, "Path should exist after initial sync");

        // Edit year on server: 2024 -> 2025
        await new EditSongFlow(songA.Title, new(Year: 2025)).ExecuteAsync(Page);

        // Second sync downloads the metadata change
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBeSuccessful();
        result2.UpdateLocal.ShouldBe(1, "Should download the year-changed song");

        // File should stay in the original 2024 folder (original_folder preserved)
        App.FileShouldExist(createdPathA, "File should remain in original folder after year change");

        // Local file metadata should reflect the new year (2025)
        var fullPath = App.GetSongPath(createdPathA);
        await FileValidator.AssertMetadataAsync(fullPath, year: 2025);
    }

    [Fact]
    public async Task Sync_ShouldDownloadWithYearPath_WhenSongAddedToDeviceAfterServerCreation()
    {
        // Set naming template to use original_folder or year fallback
        var namingTemplate = "{{ original_folder ?? year }}/{{ title }}{{ extension }}";
        await App.SetNamingTemplateAsync(namingTemplate);

        // Seed song on server WITHOUT device association
        var songA = SongsFixture.DefaultSongs[1]; // The Alibi, year 2024
        var songsData = await ServerSongs.SeedAsync(RequestContext, UserId, [songA]);
        var songData = songsData[0];

        // Use flow to add song to device via UI
        await new ManageSongDevicesFlow(songData.Title, App.DeviceName, "Add").ExecuteAsync(Page);

        // Run CLI sync to download the song to the device
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.CreateLocal.ShouldBe(1);

        // Assert file downloaded to year-based path (no original_folder, so fallback to year)
        var expectedPath = $"{songA.Year}/{songA.Title}.mp3";
        App.FileShouldExist(expectedPath, "Song should be downloaded with year-based path");
        await FileValidator.AssertMetadataAsync(App.GetSongPath(expectedPath), title: songData.Title);
    }
}
