using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Playlists;

/// <summary>
/// Playlist sharing: sharing a playlist implicitly shares (read-only) every song in it with the recipient.
/// </summary>
public class PlaylistSharingTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private const string PlaylistName = "Shared Playlist";
    private const string PrivatePlaylistName = "Private Playlist";

    private readonly SongsFixture _songs = new();
    private readonly PlaylistsFixture _playlists = new();

    protected override int UserCount => 2;

    [Fact]
    public async Task Recipient_ShouldSeeSongsOfSharedPlaylistInSharedView()
    {
        // Seed a song in a playlist owned by the sharer (user 0) and share the playlist with the recipient
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, song);

        // Switch to the recipient and reload to fetch sharers
        await SwitchUserAsync(1, reloadPage: true);

        // The song should appear in the sharer's shared view, but not in the recipient's own library
        await new ValidateSharedViewFlow(Users[0].Id, shown: [song.Title]).ExecuteAsync(Page);
        await new ShouldSongExistFlow(song.Title, shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Recipient_ShouldNotSeeSongsOutsideSharedPlaylists()
    {
        // Seed two songs, but only put the first one in the playlist shared with the recipient
        var shared = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var unshared = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, shared);

        // Switch to the recipient and reload to fetch sharers
        await SwitchUserAsync(1, reloadPage: true);

        // Only the song in the shared playlist should appear in the shared view
        await new ValidateSharedViewFlow(Users[0].Id, shown: [shared.Title], hidden: [unshared.Title]).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Recipient_ShouldSeeSharedPlaylistMarkedAsSharedWithThem()
    {
        // Seed a playlist owned by the sharer (user 0), already shared with the recipient
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, song);

        // Switch to the recipient: the playlist should be listed and marked as shared with them
        await SwitchUserAsync(1, reloadPage: true);
        await new ShouldPlaylistExistFlow(PlaylistName, shouldExist: true).ExecuteAsync(Page);
        await new ValidatePlaylistShareStateFlow((PlaylistName, PlaylistShareIndicatorComponent.SharedWithMe)).ExecuteAsync(Page);

        // The detail page should credit the sharer, list its song, and be read-only
        await new ValidateSharedPlaylistDetailsFlow(PlaylistName, Users[0].UserName, song.Title).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Owner_ShouldSeeSharedPlaylistMarkedAsSharedByThem()
    {
        // Seed a song in two playlists and share only the first one
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, song);
        await _playlists.SeedWithSongsAsync(RequestContext, UserId, PrivatePlaylistName, song);

        // The playlists page should mark only the shared playlist as shared by the owner
        await new ValidatePlaylistShareStateFlow(
            (PlaylistName, PlaylistShareIndicatorComponent.SharedByMe),
            (PrivatePlaylistName, null)).ExecuteAsync(Page);

        // The Manage Playlists dialog should mark them the same way
        await new ValidateManagePlaylistsShareStateFlow(song.Title,
            (PlaylistName, PlaylistShareIndicatorComponent.SharedByMe),
            (PrivatePlaylistName, null)).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Recipient_CanImportSongFromSharedPlaylist()
    {
        // Seed a playlist owned by the sharer (user 0), already shared with the recipient
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, song);

        // Switch to the recipient and import the shared song via the Actions menu
        await SwitchUserAsync(1, reloadPage: true);
        await new ImportSharedSongFlow(Users[0].Id, song.Title).ExecuteAsync(Page);

        // The song should now appear in the recipient's own library, as a distinct copy
        await new ShouldSongExistFlow(song.Title, shouldExist: true).ExecuteAsync(Page);
        var details = await new OpenSongDetailsFlow(song.Title).ExecuteAsync(Page);
        (await details.GetIdAsync()).ShouldNotBe(song.Id, "Imported song should have a distinct id from the sharer's song");
    }

    [Fact]
    public async Task SharersSubMenu_ShouldAppearOnlyWhenAPlaylistIsShared()
    {
        // Seed a playlist as the sharer (user 0), without sharing it yet
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var playlist = await _playlists.SeedWithSongsAsync(RequestContext, UserId, PlaylistName, song);

        // The recipient should have no Songs sub-menus while nothing is shared with them
        await SwitchUserAsync(1, reloadPage: true);
        await new ShouldSharerSubMenuExistFlow(shouldExist: false).ExecuteAsync(Page);

        // Share the playlist as the sharer
        await SwitchUserAsync(0);
        await _playlists.ShareAsync(RequestContext, playlist.Id, Users[1].Id);

        // The recipient should now see the sharer sub-menu
        await SwitchUserAsync(1, reloadPage: true);
        await new ShouldSharerSubMenuExistFlow(shouldExist: true, Users[0].Id).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Owner_CanRevokePlaylistShare()
    {
        // Seed a playlist as the sharer (user 0) and share it with the recipient through the Share dialog
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _playlists.SeedWithSongsAsync(RequestContext, UserId, PlaylistName, song);
        await new SharePlaylistFlow(PlaylistName, Users[1].UserName).ExecuteAsync(Page);

        // Revoke the share through the same dialog
        await new SharePlaylistFlow(PlaylistName, Users[1].UserName, ShareAction.Remove).ExecuteAsync(Page);

        // The recipient should no longer see the playlist nor any sharer sub-menu
        await SwitchUserAsync(1, reloadPage: true);
        await new ShouldSharerSubMenuExistFlow(shouldExist: false).ExecuteAsync(Page);
        await new ShouldPlaylistExistFlow(PlaylistName, shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task RemovingSongFromSharedPlaylist_ShouldStopSharingIt()
    {
        // Seed two songs in a shared playlist, so the shared view persists after removing one
        var kept = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var removed = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, kept, removed);

        // Remove the second song from the shared playlist as the owner
        await new RemoveSongFromPlaylistFlow(removed.Title, PlaylistName).ExecuteAsync(Page);

        // The recipient should only see the song that is still in the shared playlist
        await SwitchUserAsync(1, reloadPage: true);
        await new ValidateSharedViewFlow(Users[0].Id, shown: [kept.Title], hidden: [removed.Title]).ExecuteAsync(Page);
    }

    [Fact]
    public async Task AlbumAndArtistPages_ShouldBeAccessibleByLinkForSharedSongs()
    {
        // Seed a playlist owned by the sharer (user 0), already shared with the recipient
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await _playlists.SeedSharedAsync(RequestContext, UserId, PlaylistName, Users[1].Id, song);

        // Switch to the recipient: following the shared song's album and artist links should show the song
        await SwitchUserAsync(1, reloadPage: true);
        await new ShouldSongExistOnLinkedPageFlow(song.Title, SongLinkedTarget.Album, sharerId: Users[0].Id).ExecuteAsync(Page);
        await new ShouldSongExistOnLinkedPageFlow(song.Title, SongLinkedTarget.Artist, sharerId: Users[0].Id).ExecuteAsync(Page);
    }
}
