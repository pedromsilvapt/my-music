using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Playlists;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Playlists;

public class PlaylistShareServiceSpecs
{
    private readonly PlaylistShareManageService _manageService = new();
    private readonly PlaylistShareListService _listService = new();
    private readonly SharerListService _sharerListService = new();

    private static PlaylistShareAction Add(User user) =>
        new() { UserId = user.Id, Action = PlaylistShareActionType.Add };

    private static PlaylistShareAction Remove(User user) =>
        new() { UserId = user.Id, Action = PlaylistShareActionType.Remove };

    #region ManageSharesAsync

    [Fact]
    public async Task ManageShares_Add_CreatesOneRowPerPlaylist()
    {
        // Arrange — owner has two playlists, recipient has none shared yet
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var p1 = scenario.CreatePlaylist("P1");
        var p2 = scenario.CreatePlaylist("P2");

        // Act — share both playlists with the recipient
        var (created, removed) = await _manageService.ManageSharesAsync(
            scenario.DbContext, [p1.Id, p2.Id], [Add(recipient)], scenario.AdminUser.Id, CancellationToken.None);

        // Assert — one share row per playlist
        created.ShouldBe(2);
        removed.ShouldBe(0);
        (await scenario.DbContext.PlaylistSharings.CountAsync(ps => ps.UserId == recipient.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task ManageShares_AddTwice_IsIdempotent()
    {
        // Arrange — playlist already shared with the recipient
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("P1");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, recipient);

        // Act — share again
        var (created, _) = await _manageService.ManageSharesAsync(
            scenario.DbContext, [playlist.Id], [Add(recipient)], scenario.AdminUser.Id, CancellationToken.None);

        // Assert — no duplicate row
        created.ShouldBe(0);
        (await scenario.DbContext.PlaylistSharings.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ManageShares_MixedActions_AddsAndRemoves()
    {
        // Arrange — playlist shared with alice only
        var scenario = new Scenario();
        var alice = scenario.CreateUser("Alice", "alice");
        var bob = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("P1");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, alice);

        // Act — unshare from alice, share with bob; removing a missing share is a no-op
        var (created, removed) = await _manageService.ManageSharesAsync(
            scenario.DbContext, [playlist.Id], [Remove(alice), Add(bob), Remove(bob)], scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert — alice's share removed, bob's share added then removed within the same batch
        created.ShouldBe(1);
        removed.ShouldBe(2);
        (await scenario.DbContext.PlaylistSharings.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ManageShares_NotOwner_ThrowsUnauthorized()
    {
        // Arrange — the playlist belongs to another user
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("Not mine", ownerId: other.Id);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(() => _manageService.ManageSharesAsync(
            scenario.DbContext, [playlist.Id], [Add(recipient)], scenario.AdminUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ManageShares_MissingPlaylist_ThrowsInvalidOperation()
    {
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");

        await Should.ThrowAsync<InvalidOperationException>(() => _manageService.ManageSharesAsync(
            scenario.DbContext, [9999], [Add(recipient)], scenario.AdminUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ManageShares_WithSelf_ThrowsInvalidOperation()
    {
        var scenario = new Scenario();
        var playlist = scenario.CreatePlaylist("P1");

        await Should.ThrowAsync<InvalidOperationException>(() => _manageService.ManageSharesAsync(
            scenario.DbContext, [playlist.Id], [Add(scenario.AdminUser)], scenario.AdminUser.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task ManageShares_MissingUser_ThrowsInvalidOperation()
    {
        var scenario = new Scenario();
        var playlist = scenario.CreatePlaylist("P1");

        await Should.ThrowAsync<InvalidOperationException>(() => _manageService.ManageSharesAsync(
            scenario.DbContext, [playlist.Id],
            [new PlaylistShareAction { UserId = 9999, Action = PlaylistShareActionType.Add }],
            scenario.AdminUser.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(PlaylistType.Queue)]
    [InlineData(PlaylistType.Favorites)]
    public async Task ManageShares_SystemPlaylist_ThrowsInvalidOperation(PlaylistType type)
    {
        // Arrange — queues and favorites are personal and cannot be shared
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("System", type: type);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _manageService.ManageSharesAsync(
            scenario.DbContext, [playlist.Id], [Add(recipient)], scenario.AdminUser.Id, CancellationToken.None));
        (await scenario.DbContext.PlaylistSharings.CountAsync()).ShouldBe(0);
    }

    #endregion

    #region ListSharesAsync

    [Fact]
    public async Task ListShares_ReturnsSharesOfRequestedPlaylistsOnly()
    {
        // Arrange — two playlists shared with different users, a third one not requested
        var scenario = new Scenario();
        var alice = scenario.CreateUser("Alice", "alice");
        var bob = scenario.CreateUser("Bob", "bob");
        var p1 = scenario.CreatePlaylist("P1");
        var p2 = scenario.CreatePlaylist("P2");
        var p3 = scenario.CreatePlaylist("P3");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, p1, alice);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, p2, bob);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, p3, bob);

        // Act
        var shares = await _listService.ListSharesAsync(
            scenario.DbContext, [p1.Id, p2.Id], scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        shares.Select(s => (s.PlaylistId, s.Username)).OrderBy(s => s.PlaylistId)
            .ShouldBe([(p1.Id, "alice"), (p2.Id, "bob")]);
    }

    [Fact]
    public async Task ListShares_NotOwner_ThrowsUnauthorized()
    {
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var playlist = scenario.CreatePlaylist("Not mine", ownerId: other.Id);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => _listService.ListSharesAsync(
            scenario.DbContext, [playlist.Id], scenario.AdminUser.Id, CancellationToken.None));
    }

    #endregion

    #region ListSharersAsync

    [Fact]
    public async Task ListSharers_ReturnsDistinctPlaylistOwners()
    {
        // Arrange — alice shares two playlists with me, bob shares one, carol shares with someone else
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var alice = scenario.CreateUser("Alice", "alice");
        var bob = scenario.CreateUser("Bob", "bob");
        var carol = scenario.CreateUser("Carol", "carol");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("A1", alice.Id), me);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("A2", alice.Id), me);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("B1", bob.Id), me);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("C1", carol.Id), bob);

        // Act
        var sharers = await _sharerListService.ListSharersAsync(scenario.DbContext, me.Id, CancellationToken.None);

        // Assert — each sharer listed once, carol excluded
        sharers.Select(s => s.Username).OrderBy(u => u).ShouldBe(["alice", "bob"]);
    }

    #endregion
}
