using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class ArtistsControllerSpecs
{
    private static ArtistsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        return new ArtistsController(
            Substitute.For<ILogger<ArtistsController>>(),
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
    public async Task List_NoOwnerId_ReturnsOwnArtists()
    {
        // Arrange — current user owns two artists (one per song); another user owns one (not shared)
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("My Song A");
        scenario.CreateSong("My Song B");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None);

        // Assert — only the current user's own artists are returned
        var names = response.Artists.Select(a => a.Name).ToList();
        names.Count.ShouldBe(2);
        names.ShouldContain("My Song A Artist");
        names.ShouldContain("My Song B Artist");
        names.ShouldNotContain("Other Song Artist");
    }

    [Fact]
    public async Task List_OwnerIdIsOther_WithSharedSong_ReturnsLinkedArtists()
    {
        // Arrange — another user owns two artists (one per song); only one song is shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id);
        scenario.CreateSong("Unshared Song", ownerId: other.Id);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — only the artist linked (via SongArtist.Song) to a shared song is returned
        var names = response.Artists.Select(a => a.Name).ToList();
        names.ShouldBe(["Shared Song Artist"]);
    }

    [Fact]
    public async Task List_OwnerIdIsOther_WithoutShare_ReturnsEmpty()
    {
        // Arrange — another user owns an artist but no song of theirs is shared with me
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — nothing is visible (no share row gates the recipient in)
        response.Artists.ShouldBeEmpty();
    }
}