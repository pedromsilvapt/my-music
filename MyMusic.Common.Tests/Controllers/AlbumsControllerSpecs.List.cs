using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class AlbumsControllerSpecs
{
    private static AlbumsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        return new AlbumsController(
            Substitute.For<ILogger<AlbumsController>>(),
            currentUser);
    }

    private static SongSharing Share(Song song, User recipient, MusicDbContext db)
    {
        var sharing = new SongSharing
        {
            SongId = song.Id,
            UserId = recipient.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.SongSharings.Add(sharing);
        db.SaveChanges();
        return sharing;
    }

    [Fact]
    public async Task List_NoOwnerId_ReturnsOwnAlbums()
    {
        // Arrange — current user owns two albums (via two songs); another user owns one (not shared)
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("My Song A");
        scenario.CreateSong("My Song B");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None);

        // Assert — only the current user's own albums are returned
        var names = response.Albums.Select(a => a.Name).ToList();
        names.Count.ShouldBe(2);
        names.ShouldContain("My Song A Album");
        names.ShouldContain("My Song B Album");
        names.ShouldNotContain("Other Song Album");
    }

    [Fact]
    public async Task List_OwnerIdIsOther_WithSharedSong_ReturnsLinkedAlbums()
    {
        // Arrange — another user owns two albums; only one holds a song shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id);
        scenario.CreateSong("Unshared Song", ownerId: other.Id);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — only the album linked to a shared song is returned (gate-by-sharing)
        var names = response.Albums.Select(a => a.Name).ToList();
        names.ShouldBe(["Shared Song Album"]);
    }

    [Fact]
    public async Task List_OwnerIdIsOther_WithoutShare_ReturnsEmpty()
    {
        // Arrange — another user owns an album but no song in it is shared with me
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — nothing is visible (no share row gates the recipient in)
        response.Albums.ShouldBeEmpty();
    }
}