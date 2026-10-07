using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    // Scenario: Moving a song to another album on the server moves its file on the device
    //   Given a song on the server was downloaded to the device
    //   When the song's album is changed on the server
    //   And the CLI sync runs
    //   Then the local file is updated and moved to the folder of the new album
    //   And the file in the old album's folder is gone
    [Fact]
    public async Task Sync_ShouldRenameFileWhenAlbumChanges()
    {
        // Seed song on server associated with this device
        var serverSongs = await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify file exists with original album directory
        var originalPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(originalPath).ShouldBeTrue();

        // Change the album on the server via the edit flow
        await new EditSongFlow("Wicker Woman", new(Album: "New Album Name")).ExecuteAsync(Page);

        // Run sync again - should rename the file to new album directory
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Old path should be removed
        App.FileExists(originalPath).ShouldBeFalse();

        // New path should exist with the new album in directory structure
        var newPath = "Freya Ridings/New Album Name/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(newPath).ShouldBeTrue();
    }

    // Scenario: Adding an artist to a song on the server renames its file on the device
    //   Given a song on the server was downloaded to the device
    //   When another artist is added to the song on the server
    //   And the CLI sync runs
    //   Then the local file is updated and renamed to mention both artists
    //   And it stays in the folder of its album artist
    [Fact]
    public async Task Sync_ShouldRenameFileWhenArtistChanges()
    {
        // Seed song on server associated with this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[2] with { DeviceIds = [App.DeviceId] }]);

        // Run sync to download the song
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1);

        // Verify file exists with original artist in path
        var originalPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings.mp3";
        App.FileExists(originalPath).ShouldBeTrue();

        // Add a new artist to the song (keeping the album artist Freya Ridings)
        await new EditSongFlow("Wicker Woman", new(Artists: ["Freya Ridings", "New Artist"])).ExecuteAsync(Page);

        // Run sync again - should rename file with new artist in path
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateLocal: 1, rename: 1);

        // Old path should be removed
        App.FileExists(originalPath).ShouldBeFalse();

        // New path should exist with the added artist in the filename
        // Album artist stays as Freya Ridings, so path remains under Freya Ridings/
        // The filename includes both artists alphabetically: "Freya Ridings, New Artist"
        var newPath = "Freya Ridings/Wicker Woman/Wicker Woman - Freya Ridings, New Artist.mp3";
        App.FileExists(newPath).ShouldBeTrue();
    }
}
