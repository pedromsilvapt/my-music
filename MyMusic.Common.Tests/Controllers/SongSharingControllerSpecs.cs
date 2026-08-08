using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.SongSharing;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SongSharingControllerSpecs
{
    private SongSharingController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var songShareService = new SongShareService();
        var sharedSongImportService = Substitute.For<ISharedSongImportService>();

        return new SongSharingController(
            currentUser,
            songShareService,
            sharedSongImportService,
            Substitute.For<ILogger<SongSharingController>>());
    }

    [Fact]
    public async Task List_WhenNotOwner_Returns403()
    {
        // Arrange — a song owned by another user
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other Song", ownerId: otherUser.Id);

        var controller = CreateController(scenario);

        // Act
        var result = await controller.List(otherSong.Id, scenario.DbContext, CancellationToken.None);

        // Assert — non-owner is forbidden from listing shares
        result.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Create_WhenValid_Returns200WithShareId()
    {
        // Arrange — owner shares their song with another user
        var scenario = new Scenario();
        var ownerSong = scenario.CreateSong("My Song");
        var targetUser = scenario.CreateUser("Recipient", "recipient");

        var controller = CreateController(scenario);

        // Act
        var result = await controller.Create(
            ownerSong.Id,
            new CreateSongShareRequest { UserId = targetUser.Id },
            scenario.DbContext,
            CancellationToken.None);

        // Assert — a new share row is created and its Id returned with HTTP 200 (idempotent semantics)
        // Returning a bare DTO from an ActionResult<T> action surfaces as result.Value in unit tests
        // (the MVC pipeline would wrap it in OkObjectResult at request time).
        var actionValue = result.Value.ShouldBeOfType<CreateSongShareResponse>();
        actionValue.ShareId.ShouldBeGreaterThan(0);

        var sharing = await scenario.DbContext.SongSharings.SingleAsync();
        sharing.UserId.ShouldBe(targetUser.Id);
        sharing.SongId.ShouldBe(ownerSong.Id);
    }

    [Fact]
    public async Task Create_WhenDuplicate_Returns200WithExistingShareId()
    {
        // Arrange — an existing share row for the (song, user) pair
        var scenario = new Scenario();
        var ownerSong = scenario.CreateSong("My Song");
        var targetUser = scenario.CreateUser("Recipient", "recipient");

        var existingSharing = new SongSharing
        {
            SongId = ownerSong.Id,
            UserId = targetUser.Id,
            CreatedAt = DateTime.UtcNow,
        };
        scenario.DbContext.SongSharings.Add(existingSharing);
        scenario.DbContext.SaveChanges();

        var controller = CreateController(scenario);

        // Act — duplicate create must be idempotent (no new row, no exception)
        var result = await controller.Create(
            ownerSong.Id,
            new CreateSongShareRequest { UserId = targetUser.Id },
            scenario.DbContext,
            CancellationToken.None);

        // Assert — same existing Id is returned, still a single row
        var actionValue = result.Value.ShouldBeOfType<CreateSongShareResponse>();
        actionValue.ShareId.ShouldBe(existingSharing.Id);

        var count = await scenario.DbContext.SongSharings.CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_WhenNotOwner_Returns403()
    {
        // Arrange — a song owned by another user
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other Song", ownerId: otherUser.Id);

        var controller = CreateController(scenario);

        // Act
        var result = await controller.Delete(otherSong.Id, otherUser.Id, scenario.DbContext, CancellationToken.None);

        // Assert — non-owner cannot revoke shares
        result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ListSharers_ReturnsDistinctUsersWhoSharedWithMe()
    {
        // Arrange — two shares from the same sharer to me, plus one from a second sharer
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var sharer1 = scenario.CreateUser("Sharer One", "sharer1");
        var sharer2 = scenario.CreateUser("Sharer Two", "sharer2");

        var song1 = scenario.CreateSong("Song 1", ownerId: sharer1.Id);
        var song2 = scenario.CreateSong("Song 2", ownerId: sharer1.Id);
        var song3 = scenario.CreateSong("Song 3", ownerId: sharer2.Id);

        scenario.DbContext.SongSharings.AddRange(
            new SongSharing { SongId = song1.Id, UserId = me.Id, CreatedAt = DateTime.UtcNow },
            new SongSharing { SongId = song2.Id, UserId = me.Id, CreatedAt = DateTime.UtcNow },
            new SongSharing { SongId = song3.Id, UserId = me.Id, CreatedAt = DateTime.UtcNow });
        scenario.DbContext.SaveChanges();

        var controller = CreateController(scenario);

        // Act
        var result = await controller.ListSharers(scenario.DbContext, CancellationToken.None);

        // Assert — two distinct sharers despite three share rows (sharer1 contributed two)
        result.Sharers.Count.ShouldBe(2);
        result.Sharers.Select(s => s.Username).ShouldBe(["sharer1", "sharer2"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListBatch_WhenNotOwner_Returns403()
    {
        // Arrange — a song owned by another user; current user is AdminUser
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other Song", ownerId: otherUser.Id);

        var controller = CreateController(scenario);

        // Act
        var result = await controller.ListBatch(
            otherSong.Id.ToString(),
            scenario.DbContext,
            CancellationToken.None);

        // Assert — non-owner is forbidden from listing shares
        result.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Manage_WhenValid_Returns200WithCounts()
    {
        // Arrange — owner shares two songs with one recipient via the batch endpoint
        var scenario = new Scenario();
        var ownerSong1 = scenario.CreateSong("My Song 1");
        var ownerSong2 = scenario.CreateSong("My Song 2");
        var targetUser = scenario.CreateUser("Recipient", "recipient");

        var controller = CreateController(scenario);

        // Act
        var result = await controller.Manage(
            new ManageSongSharesRequest
            {
                SongIds = [ownerSong1.Id, ownerSong2.Id],
                Shares =
                [
                    new SongShareActionItem { UserId = targetUser.Id, Action = "Add" },
                ],
            },
            scenario.DbContext,
            CancellationToken.None);

        // Assert — 2 songs × 1 recipient = 2 created, 0 removed; HTTP 200 semantics (value surfaced directly)
        var value = result.Value.ShouldBeOfType<ManageSongSharesResponse>();
        value.Created.ShouldBe(2);
        value.Removed.ShouldBe(0);

        var count = await scenario.DbContext.SongSharings.CountAsync();
        count.ShouldBe(2);
    }

    [Fact]
    public async Task Manage_WhenNotOwner_Returns403()
    {
        // Arrange — songs owned by another user
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other Song", ownerId: otherUser.Id);
        var targetUser = scenario.CreateUser("Recipient", "recipient");

        var controller = CreateController(scenario);

        // Act
        var result = await controller.Manage(
            new ManageSongSharesRequest
            {
                SongIds = [otherSong.Id],
                Shares =
                [
                    new SongShareActionItem { UserId = targetUser.Id, Action = "Add" },
                ],
            },
            scenario.DbContext,
            CancellationToken.None);

        // Assert — non-owner cannot manage shares
        result.Result.ShouldBeOfType<ForbidResult>();
    }
}