using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Extensions;
using Shouldly;

namespace MyMusic.Common.Tests.Extensions;

/// <summary>
/// Verifies the implicit song sharing rule: a song is shared with a user when it belongs to at
/// least one playlist, owned by the song's owner, that is shared with that user.
/// </summary>
public class SongSharingRuleSpecs
{
    private static Task<List<long>> AccessibleSongIds(Scenario scenario, long userId) =>
        scenario.DbContext.Songs.WhereAccessibleBy(userId).Select(s => s.Id).ToListAsync();

    [Fact]
    public async Task SongInSharedPlaylist_IsAccessibleByRecipient()
    {
        // Arrange — owner shares a playlist containing one of their two songs
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var shared = scenario.CreateSong("Shared");
        scenario.CreateSong("Not shared");
        SharingTestHelpers.ShareSongs(scenario.DbContext, recipient, shared);

        // Act & Assert — only the song in the shared playlist is visible to the recipient
        (await AccessibleSongIds(scenario, recipient.Id)).ShouldBe([shared.Id]);
    }

    [Fact]
    public async Task ThirdPartySongInSharedPlaylist_IsNotShared()
    {
        // Arrange — carol shared a song with the admin; the admin put it in a playlist shared with bob
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var carol = scenario.CreateUser("Carol", "carol");
        var carolsSong = scenario.CreateSong("Carol's", ownerId: carol.Id);
        SharingTestHelpers.ShareSongs(scenario.DbContext, scenario.AdminUser, carolsSong);
        var playlist = scenario.CreatePlaylist("Admin's");
        scenario.AddSongToPlaylist(playlist, carolsSong, 1000);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, bob);

        // Act & Assert — the admin cannot re-share carol's song with bob
        (await AccessibleSongIds(scenario, bob.Id)).ShouldBeEmpty();
        (await AccessibleSongIds(scenario, scenario.AdminUser.Id)).ShouldBe([carolsSong.Id]);
    }

    [Fact]
    public async Task RemovingSongFromSharedPlaylist_RevokesAccess()
    {
        // Arrange — song shared through a playlist
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var song = scenario.CreateSong("Song");
        var sharing = SharingTestHelpers.ShareSongs(scenario.DbContext, recipient, song);
        (await AccessibleSongIds(scenario, recipient.Id)).ShouldBe([song.Id]);

        // Act — remove the song from the playlist
        scenario.DbContext.PlaylistSongs.RemoveRange(
            scenario.DbContext.PlaylistSongs.Where(ps => ps.PlaylistId == sharing.PlaylistId));
        await scenario.DbContext.SaveChangesAsync();

        // Assert — no longer shared
        (await AccessibleSongIds(scenario, recipient.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task UnsharingPlaylist_RevokesAccess()
    {
        // Arrange — song shared through a playlist
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var song = scenario.CreateSong("Song");
        var sharing = SharingTestHelpers.ShareSongs(scenario.DbContext, recipient, song);

        // Act — unshare the playlist
        scenario.DbContext.PlaylistSharings.Remove(sharing);
        await scenario.DbContext.SaveChangesAsync();

        // Assert — no longer shared
        (await AccessibleSongIds(scenario, recipient.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SongInTwoSharedPlaylists_StaysSharedWhenOneIsUnshared()
    {
        // Arrange — the same song in two playlists, both shared with the recipient
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var song = scenario.CreateSong("Song");
        var first = SharingTestHelpers.ShareSongs(scenario.DbContext, recipient, song);
        SharingTestHelpers.ShareSongs(scenario.DbContext, recipient, song);

        // Act — unshare only the first playlist
        scenario.DbContext.PlaylistSharings.Remove(first);
        await scenario.DbContext.SaveChangesAsync();

        // Assert — still shared through the second playlist
        (await AccessibleSongIds(scenario, recipient.Id)).ShouldBe([song.Id]);
    }

    [Fact]
    public async Task SharedWithAnotherUser_IsNotAccessible()
    {
        // Arrange — playlist shared with bob only
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var carol = scenario.CreateUser("Carol", "carol");
        var song = scenario.CreateSong("Song");
        SharingTestHelpers.ShareSongs(scenario.DbContext, bob, song);

        // Act & Assert
        (await AccessibleSongIds(scenario, carol.Id)).ShouldBeEmpty();
    }
}
