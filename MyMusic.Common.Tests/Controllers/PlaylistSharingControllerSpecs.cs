using Microsoft.AspNetCore.Mvc;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Playlists;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.PlaylistSharing;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class PlaylistSharingControllerSpecs
{
    private static PlaylistSharingController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        return new PlaylistSharingController(
            currentUser,
            new PlaylistShareListService(),
            new PlaylistShareManageService(),
            new SharerListService(),
            Substitute.For<ISharedSongImportService>());
    }

    [Fact]
    public async Task Manage_Valid_ReturnsCounts()
    {
        // Arrange
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("P1");
        var controller = CreateController(scenario);

        // Act
        var result = await controller.Manage(new ManagePlaylistSharesRequest
        {
            PlaylistIds = [playlist.Id],
            Shares = [new PlaylistShareActionItem { UserId = recipient.Id, Action = "Add" }],
        }, scenario.DbContext, CancellationToken.None);

        // Assert
        result.Value!.Created.ShouldBe(1);
        result.Value.Removed.ShouldBe(0);
    }

    [Fact]
    public async Task Manage_InvalidAction_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("P1");
        var controller = CreateController(scenario);

        var result = await controller.Manage(new ManagePlaylistSharesRequest
        {
            PlaylistIds = [playlist.Id],
            Shares = [new PlaylistShareActionItem { UserId = recipient.Id, Action = "Toggle" }],
        }, scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Manage_NotOwner_ReturnsForbid()
    {
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("Not mine", ownerId: other.Id);
        var controller = CreateController(scenario);

        var result = await controller.Manage(new ManagePlaylistSharesRequest
        {
            PlaylistIds = [playlist.Id],
            Shares = [new PlaylistShareActionItem { UserId = recipient.Id, Action = "Add" }],
        }, scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ListBatch_ReturnsShares()
    {
        var scenario = new Scenario();
        var recipient = scenario.CreateUser("Bob", "bob");
        var playlist = scenario.CreatePlaylist("P1");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, playlist, recipient);
        var controller = CreateController(scenario);

        var result = await controller.ListBatch($"{playlist.Id}", scenario.DbContext, CancellationToken.None);

        result.Value!.Shares.ShouldHaveSingleItem().UserId.ShouldBe(recipient.Id);
    }

    [Fact]
    public async Task ListBatch_NoIds_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var result = await controller.ListBatch("", scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListBatch_NotOwner_ReturnsForbid()
    {
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var playlist = scenario.CreatePlaylist("Not mine", ownerId: other.Id);
        var controller = CreateController(scenario);

        var result = await controller.ListBatch($"{playlist.Id}", scenario.DbContext, CancellationToken.None);

        result.Result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ListSharers_ReturnsPlaylistOwnersSharingWithMe()
    {
        var scenario = new Scenario();
        var alice = scenario.CreateUser("Alice", "alice");
        SharingTestHelpers.SharePlaylist(scenario.DbContext, scenario.CreatePlaylist("A1", alice.Id), scenario.AdminUser);
        var controller = CreateController(scenario);

        var result = await controller.ListSharers(scenario.DbContext, CancellationToken.None);

        result.Sharers.ShouldHaveSingleItem().Username.ShouldBe("alice");
    }
}
