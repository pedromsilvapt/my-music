using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Cli;

public partial class CliSyncTests
{
    [Fact]
    public async Task Sync_ShouldMergeSongsWhenChecksumsMatch()
    {
        var baseSong = SongsFixture.DefaultSongs[1];
        var properAlbumSong = baseSong;
        var lowerAlbumSong = baseSong with { Title = baseSong.Title.ToLower() };

        // Setup: Create two local files with different album casing
        await _cli.CreateSongAsync(properAlbumSong);
        var lowerAlbumPath = await _cli.CreateSongAsync(lowerAlbumSong);

        // First sync: Should create two separate songs due to different album names
        var result1 = await CliRunner.SyncAsync(_cli);
        result1.ShouldBeSuccessful();
        result1.Created.ShouldBe(2, $"Expected 2 songs to be created but got {result1.Created}");

        // Validate two songs exist on the server
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);

        // Add first song to "Proper Casing" playlist (row 0)
        var properPlaylist = await _playlists.SeedAsync(RequestContext, UserId, "Proper Casing");
        await new AddSongToPlaylistFlow(properAlbumSong.Title, properPlaylist.Name).ExecuteAsync(Page);

        // Add second song to "Lower Casing" playlist (row 1)
        var lowerPlaylist = await _playlists.SeedAsync(RequestContext, UserId, "Lower Casing");
        await new AddSongToPlaylistFlow(lowerAlbumSong.Title, lowerPlaylist.Name).ExecuteAsync(Page);

        // Now, fix the title on the second file
        await _cli.UpdateLocalFileMetadataAsync(lowerAlbumPath, new EditSongOptions(Title: properAlbumSong.Title));

        // Second sync: Should detect duplicates and merge
        var result2 = await CliRunner.SyncAsync(_cli);
        result2.ShouldBeSuccessful();

        // Validate only one song exists on the server
        songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(1, "Expected only one song after merge");

        // Validate song album is proper cased
        await new ValidateSongDetailsFlow(properAlbumSong.Title, new(Album: properAlbumSong.Album))
            .ExecuteAsync(Page);

        // Validate song belongs to device
        await new ShouldSongExistInDeviceFlow(properAlbumSong.Title, _cli.DeviceName, shouldExist: true)
            .ExecuteAsync(Page);

        // Validate song belongs to both playlists
        await new ValidateSongInPlaylistFlow(properAlbumSong.Title, "Proper Casing")
            .ExecuteAsync(Page);
        await new ValidateSongInPlaylistFlow(properAlbumSong.Title, "Lower Casing")
            .ExecuteAsync(Page);
    }
}
