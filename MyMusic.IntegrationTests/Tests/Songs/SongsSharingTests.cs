using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

public class SongsSharingTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;

    protected override int UserCount => 2;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

    [Fact]
    public async Task Recipient_ShouldSeeSharedSongsInSharedView()
    {
        // Seed a song as the sharer (user 0)
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);

        // Share it with the recipient via the Manage Sharing dialog
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload to fetch sharers
        await SwitchUserAsync(1, reloadPage: true);

        // Verify the song appears in the shared view
        await new ShouldSongExistInSharedViewFlow(song.Title, Users[0].Id, shouldExist: true)
            .ExecuteAsync(Page);

        // Verify the song does not appear in the recipient's own library
        await new ShouldSongExistFlow(song.Title, shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Recipient_ShouldNotSeeUnsharedSongs()
    {
        // Seed two songs as the sharer (user 0)
        var shared = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var unshared = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);

        // Share only the first song with the recipient
        await new ManageShareSongsFlow([shared.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload to fetch sharers
        await SwitchUserAsync(1, reloadPage: true);

        // The shared song should appear in the shared view
        await new ShouldSongExistInSharedViewFlow(shared.Title, Users[0].Id, shouldExist: true)
            .ExecuteAsync(Page);

        // The unshared song should not appear in the shared view
        await new ShouldSongExistInSharedViewFlow(unshared.Title, Users[0].Id, shouldExist: false)
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task Recipient_CanImportSharedSong()
    {
        // Seed a song as the sharer (user 0) and share it
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload
        await SwitchUserAsync(1, reloadPage: true);

        // Import the shared song via the Actions menu
        await new ImportSharedSongFlow(Users[0].Id, song.Title).ExecuteAsync(Page);

        // The song should now appear in the recipient's own library
        await new ShouldSongExistFlow(song.Title, shouldExist: true).ExecuteAsync(Page);

        // The imported song should have a distinct id from the sharer's song
        var details = await new OpenSongDetailsFlow(song.Title).ExecuteAsync(Page);
        var recipientSongId = await details.GetIdAsync();
        recipientSongId.ShouldNotBe(song.Id, "Imported song should have a distinct id from the sharer's song");
    }

    [Fact]
    public async Task Recipient_Import_IsIdempotent()
    {
        // Seed a song as the sharer (user 0) and share it
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload
        await SwitchUserAsync(1, reloadPage: true);

        // Import the shared song once
        await new ImportSharedSongFlow(Users[0].Id, song.Title).ExecuteAsync(Page);

        // Verify the song appeared in the recipient's own library
        await new ShouldSongExistFlow(song.Title, shouldExist: true).ExecuteAsync(Page);

        // Capture the recipient's song id after the first import
        var details = await new OpenSongDetailsFlow(song.Title).ExecuteAsync(Page);
        var firstImportId = await details.GetIdAsync();

        // Import the shared song a second time (should be idempotent via Skip strategy)
        await new ImportSharedSongFlow(Users[0].Id, song.Title).ExecuteAsync(Page);

        // Verify there is still only one copy in the recipient's library
        var songsPage = await new HomePage(Page).Navbar.GoToSongsAsync();
        var matchCount = await songsPage.Collection.CountRowsByTitleAsync(song.Title);
        matchCount.ShouldBe(1, "Importing twice should not create a duplicate song");

        // The song id should be the same as after the first import
        await new ValidateSongDetailsFlow(song.Title, new(SongId: firstImportId)).ExecuteAsync(Page);
    }

    [Fact]
    public async Task SharersSubMenu_ShouldAppearWhenShared()
    {
        // Seed a song as the sharer (user 0) and share it with the recipient
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload to fetch sharers
        await SwitchUserAsync(1, reloadPage: true);

        // The sharer sub-menu should now appear under the Songs nav item
        var home = new HomePage(Page);
        var hasSubMenu = await home.Navbar.HasSharedSongsLinkAsync(Users[0].Id);
        hasSubMenu.ShouldBeTrue("Sharer sub-menu should appear for the recipient after a share is created");
    }

    [Fact]
    public async Task SharersSubMenu_ShouldNotAppearWhenNoShares()
    {
        // Switch to the recipient (no shares exist yet) and reload
        await SwitchUserAsync(1, reloadPage: true);

        // The Songs nav item should have no sub-menus (no sharers)
        var home = new HomePage(Page);
        var hasSubMenus = await home.Navbar.HasSongsSubMenusAsync();
        hasSubMenus.ShouldBeFalse("Songs sub-menus should not appear when there are no shares");
    }

    [Fact]
    public async Task AlbumDetailsPage_SharedAlbumAccessibleByLink()
    {
        // Seed + share the song as user 0
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // As user 0, reach the album page the way a user would: song by name, then click the album link
        await new OpenSongLinkedDetailsFlow(song.Title, SongLinkedTarget.Album).ExecuteAsync(Page);

        // Switch to user 1 and reload the album page (now in user 1 context)
        await SwitchUserAsync(1, reloadPage: true);
        var albumPage = new AlbumDetailsPage(Page);
        await albumPage.WaitForLoadedAsync();

        // Assert the shared song appears on the album as user 1
        var rowIndex = await albumPage.Songs.FindRowByTitleAsync(song.Title);
        rowIndex.ShouldBeGreaterThanOrEqualTo(0, "Shared song should appear on the shared album");
    }

    [Fact]
    public async Task ArtistDetailsPage_SharedArtistAccessibleByLink()
    {
        // Seed + share the song as user 0
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // As user 0, reach the artist page the way a user would: song by name, then click the artist link
        await new OpenSongLinkedDetailsFlow(song.Title, SongLinkedTarget.Artist).ExecuteAsync(Page);

        // Switch to user 1 and reload the artist page (now in user 1 context)
        await SwitchUserAsync(1, reloadPage: true);
        var artistPage = new ArtistDetailsPage(Page);
        await artistPage.WaitForLoadedAsync();

        // Assert the shared song appears on the artist as user 1
        var rowIndex = await artistPage.Songs.FindRowByTitleAsync(song.Title);
        rowIndex.ShouldBeGreaterThanOrEqualTo(0, "Shared song should appear on the shared artist");
    }

    [Fact]
    public async Task Owner_CanShareMultipleSongsViaDialog()
    {
        // Seed two songs as the owner (user 0)
        var first = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var second = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);

        // Share both songs with the recipient via the floating Actions menu
        await new ManageShareSongsFlow([first.Title, second.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload to fetch sharers
        await SwitchUserAsync(1, reloadPage: true);

        // Both songs should appear in the recipient's shared view
        await new ShouldSongExistInSharedViewFlow(first.Title, Users[0].Id, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistInSharedViewFlow(second.Title, Users[0].Id, shouldExist: true).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Owner_ShareAction_HiddenForSharedWithMeView()
    {
        // Seed and share a song as the sharer (user 0)
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // Switch to the recipient and reload
        await SwitchUserAsync(1, reloadPage: true);

        // The Share action should be absent from the floating Actions menu in the shared view
        await new ShouldSongActionBeVisibleFlow(SongAction.Share, sharerId: Users[0].Id, song.Title, shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Owner_SongDetailPage_ManageSharingButton_GatedByOwnership()
    {
        // Seed and share a song as the sharer (user 0)
        var song = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        await new ManageShareSongsFlow([song.Title], Users[1].UserName).ExecuteAsync(Page);

        // The owner's song detail page shows the Manage Sharing button
        await new ShouldSongActionBeVisibleFlow(SongAction.ManageSharing, song.Id, shouldExist: true).ExecuteAsync(Page);

        // Switch to the recipient and open the sharer's song detail page via direct link
        await SwitchUserAsync(1, reloadPage: true);

        // The shared song's detail page hides the Manage Sharing button from the recipient
        await new ShouldSongActionBeVisibleFlow(SongAction.ManageSharing, song.Id, shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task Owner_CanRevokeShareViaDialog()
    {
        // Seed two songs as the sharer and share both with the recipient,
        // so the shared view persists after revoking a single song
        var kept = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var revoked = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);
        await new ManageShareSongsFlow([kept.Title, revoked.Title], Users[1].UserName).ExecuteAsync(Page);

        // Confirm the recipient sees both songs before revocation
        await SwitchUserAsync(1, reloadPage: true);
        await new ShouldSongExistInSharedViewFlow(kept.Title, Users[0].Id, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistInSharedViewFlow(revoked.Title, Users[0].Id, shouldExist: true).ExecuteAsync(Page);

        // Switch back to the owner to revoke the share for one song
        await SwitchUserAsync(0, reloadPage: true);

        // Revoke the share for the second song via the Manage Sharing dialog
        await new ManageShareSongsFlow([revoked.Title], Users[1].UserName, "remove").ExecuteAsync(Page);

        // Switch to the recipient and reload to refresh the shared view
        await SwitchUserAsync(1, reloadPage: true);

        // The revoked song is gone, but the kept song remains in the shared view
        await new ShouldSongExistInSharedViewFlow(revoked.Title, Users[0].Id, shouldExist: false).ExecuteAsync(Page);
        await new ShouldSongExistInSharedViewFlow(kept.Title, Users[0].Id, shouldExist: true).ExecuteAsync(Page);
    }
}
