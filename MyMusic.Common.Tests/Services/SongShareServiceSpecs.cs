using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SongShareServiceSpecs
{
    private static (SongShareService service, Scenario scenario) CreateService()
    {
        var scenario = new Scenario();
        var service = new SongShareService();
        return (service, scenario);
    }

    [Fact]
    public async Task CreateShareAsync_ValidInputs_CreatesRow()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient = scenario.CreateUser("Recipient", "recipient");
        var song = scenario.CreateSong("Song");

        // Act
        var shareId = await service.CreateShareAsync(
            scenario.DbContext, song.Id, recipient.Id, owner.Id, TestContext.Current.CancellationToken);

        // Assert
        shareId.ShouldBeGreaterThan(0);
        var sharing = await scenario.DbContext.SongSharings
            .FirstOrDefaultAsync(ss => ss.SongId == song.Id && ss.UserId == recipient.Id, TestContext.Current.CancellationToken);
        sharing.ShouldNotBeNull();
        sharing.Id.ShouldBe(shareId);
        sharing.CreatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CreateShareAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var otherUser = scenario.CreateUser("Other", "other");
        var recipient = scenario.CreateUser("Recipient", "recipient");
        var song = scenario.CreateSong("Song", ownerId: otherUser.Id);

        // Act & Assert
        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.CreateShareAsync(
                scenario.DbContext, song.Id, recipient.Id, scenario.AdminUser.Id, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain($"does not own song {song.Id}");
    }

    [Fact]
    public async Task CreateShareAsync_TargetIsSelf_Throws()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var song = scenario.CreateSong("Song");

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateShareAsync(
                scenario.DbContext, song.Id, owner.Id, owner.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateShareAsync_DuplicateShare_ReturnsExistingId_NoOp()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient = scenario.CreateUser("Recipient", "recipient");
        var song = scenario.CreateSong("Song");

        // Act — first share creates the row
        var firstId = await service.CreateShareAsync(
            scenario.DbContext, song.Id, recipient.Id, owner.Id, TestContext.Current.CancellationToken);

        // A duplicate call returns the existing Id without creating a new row or throwing.
        var secondId = await service.CreateShareAsync(
            scenario.DbContext, song.Id, recipient.Id, owner.Id, TestContext.Current.CancellationToken);

        // Assert
        secondId.ShouldBe(firstId);
        var count = await scenario.DbContext.SongSharings
            .CountAsync(ss => ss.SongId == song.Id && ss.UserId == recipient.Id, TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteShareAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var otherUser = scenario.CreateUser("Other", "other");
        var recipient = scenario.CreateUser("Recipient", "recipient");
        var song = scenario.CreateSong("Song", ownerId: otherUser.Id);

        // Act & Assert
        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.DeleteShareAsync(
                scenario.DbContext, song.Id, recipient.Id, scenario.AdminUser.Id, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain($"does not own song {song.Id}");
    }

    [Fact]
    public async Task ListSharesAsync_ReturnsOnlyRecipients()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient1 = scenario.CreateUser("Recipient One", "recipient1");
        var recipient2 = scenario.CreateUser("Recipient Two", "recipient2");
        var song = scenario.CreateSong("Song");
        await service.CreateShareAsync(scenario.DbContext, song.Id, recipient1.Id, owner.Id, TestContext.Current.CancellationToken);
        await service.CreateShareAsync(scenario.DbContext, song.Id, recipient2.Id, owner.Id, TestContext.Current.CancellationToken);

        // Act
        var shares = await service.ListSharesAsync(scenario.DbContext, song.Id, owner.Id, TestContext.Current.CancellationToken);

        // Assert
        shares.Count.ShouldBe(2);
        shares.Select(s => s.Username).ShouldBe(new[] { "recipient1", "recipient2" }, ignoreOrder: true);
        shares.ShouldAllBe(s => s.SongId == song.Id);
    }

    [Fact]
    public async Task ListSharersAsync_ReturnsDistinctUsersWhoSharedWithMe()
    {
        // Arrange
        var (service, scenario) = CreateService();
        var me = scenario.AdminUser;
        var sharerA = scenario.CreateUser("Sharer A", "sharerA");
        var sharerB = scenario.CreateUser("Sharer B", "sharerB");

        var songA1 = scenario.CreateSong("A1", ownerId: sharerA.Id);
        var songA2 = scenario.CreateSong("A2", ownerId: sharerA.Id);
        var songB1 = scenario.CreateSong("B1", ownerId: sharerB.Id);

        // Sharer A shares two songs with me → only one distinct sharer.
        await service.CreateShareAsync(scenario.DbContext, songA1.Id, me.Id, sharerA.Id, TestContext.Current.CancellationToken);
        await service.CreateShareAsync(scenario.DbContext, songA2.Id, me.Id, sharerA.Id, TestContext.Current.CancellationToken);

        // Act — only sharer A so far
        var sharersBefore = await service.ListSharersAsync(scenario.DbContext, me.Id, TestContext.Current.CancellationToken);

        // Assert — one distinct sharer despite two shares
        sharersBefore.Count.ShouldBe(1);
        sharersBefore[0].Username.ShouldBe("sharerA");

        // Mutate — sharer B now shares a song with me too.
        await service.CreateShareAsync(scenario.DbContext, songB1.Id, me.Id, sharerB.Id, TestContext.Current.CancellationToken);

        // Act — both sharers now
        var sharersAfter = await service.ListSharersAsync(scenario.DbContext, me.Id, TestContext.Current.CancellationToken);

        // Assert — two distinct sharers
        sharersAfter.Count.ShouldBe(2);
        sharersAfter.Select(s => s.Username).ShouldBe(new[] { "sharerA", "sharerB" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ManageSharesAsync_AddActions_CreatesRowsForAllSongUserPairs()
    {
        // Arrange — owner shares two songs with two recipients in one batch call
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient1 = scenario.CreateUser("Recipient One", "recipient1");
        var recipient2 = scenario.CreateUser("Recipient Two", "recipient2");
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");

        var songIds = new[] { song1.Id, song2.Id };
        var actions = new List<SongShareAction>
        {
            new() { UserId = recipient1.Id, Action = SongShareActionType.Add },
            new() { UserId = recipient2.Id, Action = SongShareActionType.Add },
        };

        // Act
        var (created, removed) = await service.ManageSharesAsync(
            scenario.DbContext, songIds, actions, owner.Id, TestContext.Current.CancellationToken);

        // Assert — 2 songs × 2 recipients = 4 rows created, none removed
        created.ShouldBe(4);
        removed.ShouldBe(0);
        var count = await scenario.DbContext.SongSharings.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(4);
    }

    [Fact]
    public async Task ManageSharesAsync_RemoveActions_DeletesRowsForAllSongUserPairs()
    {
        // Arrange — pre-seed shares for two songs × two recipients, then remove them all
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient1 = scenario.CreateUser("Recipient One", "recipient1");
        var recipient2 = scenario.CreateUser("Recipient Two", "recipient2");
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");

        foreach (var songId in new[] { song1.Id, song2.Id })
        {
            await service.CreateShareAsync(scenario.DbContext, songId, recipient1.Id, owner.Id, TestContext.Current.CancellationToken);
            await service.CreateShareAsync(scenario.DbContext, songId, recipient2.Id, owner.Id, TestContext.Current.CancellationToken);
        }

        var songIds = new[] { song1.Id, song2.Id };
        var actions = new List<SongShareAction>
        {
            new() { UserId = recipient1.Id, Action = SongShareActionType.Remove },
            new() { UserId = recipient2.Id, Action = SongShareActionType.Remove },
        };

        // Act
        var (created, removed) = await service.ManageSharesAsync(
            scenario.DbContext, songIds, actions, owner.Id, TestContext.Current.CancellationToken);

        // Assert — all 4 rows removed, none created
        created.ShouldBe(0);
        removed.ShouldBe(4);
        var count = await scenario.DbContext.SongSharings.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task ManageSharesAsync_MixedAddRemove_AppliesCorrectly()
    {
        // Arrange — song1 shared with recipient1 already; song2 unshared.
        // Add recipient1 to both (no-op for song1), remove recipient2 from both (no-op for song1, real for song2).
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient1 = scenario.CreateUser("Recipient One", "recipient1");
        var recipient2 = scenario.CreateUser("Recipient Two", "recipient2");
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");

        await service.CreateShareAsync(scenario.DbContext, song1.Id, recipient1.Id, owner.Id, TestContext.Current.CancellationToken);
        await service.CreateShareAsync(scenario.DbContext, song2.Id, recipient2.Id, owner.Id, TestContext.Current.CancellationToken);

        var songIds = new[] { song1.Id, song2.Id };
        var actions = new List<SongShareAction>
        {
            new() { UserId = recipient1.Id, Action = SongShareActionType.Add },
            new() { UserId = recipient2.Id, Action = SongShareActionType.Remove },
        };

        // Act
        var (created, removed) = await service.ManageSharesAsync(
            scenario.DbContext, songIds, actions, owner.Id, TestContext.Current.CancellationToken);

        // Assert — Add recipient1: created 1 (song2), skipped 1 (song1 already had it).
        // Remove recipient2: removed 1 (song2), skipped 1 (song1 had none).
        created.ShouldBe(1);
        removed.ShouldBe(1);
        var remaining = await scenario.DbContext.SongSharings
            .Where(ss => ss.UserId == recipient1.Id)
            .Select(ss => ss.SongId)
            .OrderBy(id => id)
            .ToListAsync(TestContext.Current.CancellationToken);
        remaining.ShouldBe(new[] { song1.Id, song2.Id }, ignoreOrder: true);
        var recipient2Count = await scenario.DbContext.SongSharings
            .CountAsync(ss => ss.UserId == recipient2.Id, TestContext.Current.CancellationToken);
        recipient2Count.ShouldBe(0);
    }

    [Fact]
    public async Task ManageSharesAsync_Idempotent_NoErrorOnDuplicateOrMissing()
    {
        // Arrange — song1 already shared with recipient; song2 unshared.
        // Add (duplicate for song1, new for song2) and a separate Remove on a pair with no row.
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient = scenario.CreateUser("Recipient", "recipient");
        var otherRecipient = scenario.CreateUser("Other Recipient", "otherRecipient");
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");

        await service.CreateShareAsync(scenario.DbContext, song1.Id, recipient.Id, owner.Id, TestContext.Current.CancellationToken);

        var songIds = new[] { song1.Id, song2.Id };
        var actions = new List<SongShareAction>
        {
            // Add recipient: duplicate for song1 (no-op), new for song2 → 1 created
            new() { UserId = recipient.Id, Action = SongShareActionType.Add },
            // Remove otherRecipient: missing for both songs → 0 removed, no error
            new() { UserId = otherRecipient.Id, Action = SongShareActionType.Remove },
        };

        // Act
        var (created, removed) = await service.ManageSharesAsync(
            scenario.DbContext, songIds, actions, owner.Id, TestContext.Current.CancellationToken);

        // Assert — duplicate Add skipped, missing Remove skipped; no errors thrown.
        created.ShouldBe(1);
        removed.ShouldBe(0);
        var count = await scenario.DbContext.SongSharings.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task ManageSharesAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange — songs owned by another user
        var (service, scenario) = CreateService();
        var otherUser = scenario.CreateUser("Other", "other");
        var recipient = scenario.CreateUser("Recipient", "recipient");
        var song = scenario.CreateSong("Song", ownerId: otherUser.Id);

        var actions = new List<SongShareAction>
        {
            new() { UserId = recipient.Id, Action = SongShareActionType.Add },
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.ManageSharesAsync(
                scenario.DbContext, new[] { song.Id }, actions, scenario.AdminUser.Id, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain($"does not own song {song.Id}");
    }

    [Fact]
    public async Task ManageSharesAsync_TargetIsSelf_Throws()
    {
        // Arrange — owner tries to share with themselves across multiple songs
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");

        var actions = new List<SongShareAction>
        {
            new() { UserId = owner.Id, Action = SongShareActionType.Add },
        };

        // Act & Assert — reuses per-song validation that disallows sharing with the owner
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ManageSharesAsync(
                scenario.DbContext, new[] { song1.Id, song2.Id }, actions, owner.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListSharesForSongsAsync_ReturnsAllSharesForOwnedSongs()
    {
        // Arrange — owner shares two songs with two recipients
        var (service, scenario) = CreateService();
        var owner = scenario.AdminUser;
        var recipient1 = scenario.CreateUser("Recipient One", "recipient1");
        var recipient2 = scenario.CreateUser("Recipient Two", "recipient2");
        var song1 = scenario.CreateSong("Song 1");
        var song2 = scenario.CreateSong("Song 2");

        await service.CreateShareAsync(scenario.DbContext, song1.Id, recipient1.Id, owner.Id, TestContext.Current.CancellationToken);
        await service.CreateShareAsync(scenario.DbContext, song1.Id, recipient2.Id, owner.Id, TestContext.Current.CancellationToken);
        await service.CreateShareAsync(scenario.DbContext, song2.Id, recipient1.Id, owner.Id, TestContext.Current.CancellationToken);

        // Act
        var shares = await service.ListSharesForSongsAsync(
            scenario.DbContext, new[] { song1.Id, song2.Id }, owner.Id, TestContext.Current.CancellationToken);

        // Assert — 3 rows total across the two songs, each carries its SongId
        shares.Count.ShouldBe(3);
        shares.ShouldAllBe(s => s.SongId == song1.Id || s.SongId == song2.Id);
        shares.Select(s => s.Username).ShouldBe(
            new[] { "recipient1", "recipient2", "recipient1" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ListSharesForSongsAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange — a song owned by another user
        var (service, scenario) = CreateService();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Song", ownerId: otherUser.Id);

        // Act & Assert
        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.ListSharesForSongsAsync(
                scenario.DbContext, new[] { song.Id }, scenario.AdminUser.Id, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain($"does not own song {song.Id}");
    }
}