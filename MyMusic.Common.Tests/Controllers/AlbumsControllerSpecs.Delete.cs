using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Common.Tests.Utilities;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Albums;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class AlbumsControllerDeleteSpecs
{
    private static AlbumsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new AlbumsController(Substitute.For<ILogger<AlbumsController>>(), currentUser);
    }

    private static AlbumRemoveService CreateAlbumRemoveService(Scenario scenario) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<AlbumRemoveService>>());

    [Fact]
    public async Task GetUsage_AlbumWithSongs_ReturnsItsSongsCount()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        scenario.CreateSong("First", album: album);
        scenario.CreateSong("Second", album: album);
        scenario.CreateSong("Other", album: scenario.CreateAlbum("Other Album", artist));

        // Act
        var result = await CreateController(scenario).GetUsage(new GetAlbumsUsageRequest { AlbumIds = [album.Id] }, scenario.DbContext, CancellationToken.None);

        // Assert
        result.SongsCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetUsage_SeveralAlbums_CountsTheSongsOfOwnedAlbumsOnly()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var artist = scenario.CreateArtist("Artist");
        var first = scenario.CreateAlbum("First", artist);
        var second = scenario.CreateAlbum("Second", artist);
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);
        scenario.CreateSong("One", album: first);
        scenario.CreateSong("Two", album: second);
        scenario.CreateSong("Three", album: second);
        scenario.CreateSong("Theirs", ownerId: other.Id, album: othersAlbum);

        // Act
        var result = await CreateController(scenario).GetUsage(
            new GetAlbumsUsageRequest { AlbumIds = [first.Id, second.Id, othersAlbum.Id] }, scenario.DbContext,
            CancellationToken.None);

        // Assert
        result.SongsCount.ShouldBe(3);
    }

    [Fact]
    public async Task Delete_EmptyAlbum_Returns204AndDeletesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        var result = await CreateController(scenario)
            .Delete(new DeleteAlbumsRequest { AlbumIds = [album.Id] }, CreateAlbumRemoveService(scenario), CancellationToken.None);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        scenario.DbContext.Albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_AlbumOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var result = await CreateController(scenario)
            .Delete(new DeleteAlbumsRequest { AlbumIds = [othersAlbum.Id] }, CreateAlbumRemoveService(scenario), CancellationToken.None);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        scenario.DbContext.Albums.Any(a => a.Id == othersAlbum.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_PlaceholderAlbumWithSongs_Returns409()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));
        scenario.CreateSong("Song", album: placeholder);

        // Act
        var result = await CreateController(scenario)
            .Delete(new DeleteAlbumsRequest { AlbumIds = [placeholder.Id] }, CreateAlbumRemoveService(scenario), CancellationToken.None);

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull()
            .ShouldContain("cannot be deleted while it still has songs");
    }
}
