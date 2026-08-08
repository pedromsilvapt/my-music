using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class GenresControllerSpecs
{
    private static GenresController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        return new GenresController(
            Substitute.For<ILogger<GenresController>>(),
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
    public async Task List_NoOwnerId_ReturnsOwnGenres()
    {
        // Arrange — current user owns two genres; another user owns one (not shared)
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var mySongA = scenario.CreateSong("My Song A");
        var mySongB = scenario.CreateSong("My Song B");
        var otherSong = scenario.CreateSong("Other Song", ownerId: other.Id);

        var myGenreA = scenario.CreateGenre("Rock");
        var myGenreB = scenario.CreateGenre("Jazz");
        var otherGenre = scenario.CreateGenre("Other Genre", ownerId: other.Id);

        AttachGenre(mySongA, myGenreA, scenario.DbContext);
        AttachGenre(mySongB, myGenreB, scenario.DbContext);
        AttachGenre(otherSong, otherGenre, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None);

        // Assert — only the current user's own genres are returned
        var names = response.Genres.Select(g => g.Name).ToList();
        names.Count.ShouldBe(2);
        names.ShouldContain("Rock");
        names.ShouldContain("Jazz");
        names.ShouldNotContain("Other Genre");
    }

    [Fact]
    public async Task List_OwnerIdIsOther_WithSharedSong_ReturnsLinkedGenres()
    {
        // Arrange — another user owns two genres; only one is linked to a song shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id);
        var unsharedSong = scenario.CreateSong("Unshared Song", ownerId: other.Id);

        var sharedGenre = scenario.CreateGenre("Shared Genre", ownerId: other.Id);
        var unsharedGenre = scenario.CreateGenre("Unshared Genre", ownerId: other.Id);

        AttachGenre(sharedSong, sharedGenre, scenario.DbContext);
        AttachGenre(unsharedSong, unsharedGenre, scenario.DbContext);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — only the genre linked (via SongGenre.Song) to a shared song is returned
        var names = response.Genres.Select(g => g.Name).ToList();
        names.ShouldBe(["Shared Genre"]);
    }

    [Fact]
    public async Task List_OwnerIdIsOther_WithoutShare_ReturnsEmpty()
    {
        // Arrange — another user owns a genre but no linked song is shared with me
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other Song", ownerId: other.Id);
        var otherGenre = scenario.CreateGenre("Other Genre", ownerId: other.Id);
        AttachGenre(otherSong, otherGenre, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — nothing is visible (no share row gates the recipient in)
        response.Genres.ShouldBeEmpty();
    }

    private static void AttachGenre(Song song, Genre genre, MusicDbContext db)
    {
        db.Add(new SongGenre { SongId = song.Id, GenreId = genre.Id });
        db.SaveChanges();
    }
}