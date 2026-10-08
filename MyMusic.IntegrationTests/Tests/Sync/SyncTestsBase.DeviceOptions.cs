using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: A naming template typed on the web is previewed, and applied by a sync once saved
    //   Given two songs from the device were synced to the server
    //   When the user types a naming template in the device editor, without saving it
    //   Then the editor previews the new path of the file the template would rename
    //   And the device's details page lists the sync session
    //   When the user saves the naming template on the web
    //   And the songs are edited on the server
    //   And the CLI sync runs
    //   Then the file is moved to the path that was previewed
    //   And the file whose path already matched the template stays where it is
    [Fact]
    public async Task Sync_NamingTemplateEditedOnTheWeb_ShouldRenameFilesAsPreviewed()
    {
        var namingTemplate = "{{ year }}/{{ title }} - {{ artists_label }}{{ extension }}";

        // Setup: two songs on the device, only one of them at the path the template will give it
        var songA = SongsFixture.DefaultSongs[1]; // The Alibi, year 2024
        var songB = SongsFixture.DefaultSongs[2]; // Wicker Woman, year 2025
        var pathA = $"Unsorted/{songA.Title} - {songA.Artists![0]}.mp3";
        var pathB = $"{songB.Year}/{songB.Title} - {songB.Artists![0]}.mp3";
        await App.CreateSongsAsync((songA, pathA), (songB, pathB));

        // Initial sync uploads both songs to the server
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createRemote: 2);

        // Typing the template should preview the new path of the misplaced file only, without saving anything
        var expectedPathA = $"{songA.Year}/{songA.Title} - {songA.Artists[0]}.mp3";
        await new ValidateNamingPreviewFlow(App.DeviceName, namingTemplate, total: 2, (pathA, expectedPathA))
            .ExecuteAsync(Page);

        // The device's page should list the session of the sync, and still use the default template
        await new ValidateDeviceDetailsFlow(App.DeviceName,
            new(UsesDefaultNamingTemplate: true, SongCount: 2, SessionsCount: 1)).ExecuteAsync(Page);

        // Saving the template on the web makes it the device's: the application has no template of its own
        await new EditDeviceFlow(App.DeviceName, new(NamingTemplate: namingTemplate)).ExecuteAsync(Page);

        // A file is renamed when its song changes: explicit songs keep their title, artists and year
        await new EditSongFlow(songA.Title!, new(Explicit: true)).ExecuteAsync(Page);
        await new EditSongFlow(songB.Title!, new(Explicit: true)).ExecuteAsync(Page);

        // The sync should move the misplaced file to the previewed path, and update the other one in place
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 2, rename: 1);

        App.FileShouldExist(expectedPathA);
        App.FileShouldNotExist(pathA, "The file should be moved by the template saved on the web");
        App.FileShouldExist(pathB);
    }
}
