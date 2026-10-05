using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Albums;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class AlbumsControllerCreateSpecs
{
    private static AlbumsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new AlbumsController(Substitute.For<ILogger<AlbumsController>>(), currentUser);
    }

    private static AlbumCreateService CreateAlbumCreateService(Scenario scenario) =>
        new(scenario.DbContext, scenario.AdvisoryLocks, Substitute.For<ILogger<AlbumCreateService>>());

    [Fact]
    public async Task Create_ValidRequest_ReturnsTheCreatedAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var controller = CreateController(scenario);

        // Act
        var result = await controller.Create(
            new CreateAlbumRequest { Name = "Album", ArtistId = artist.Id, Year = 1999 },
            CreateAlbumCreateService(scenario), CancellationToken.None);

        // Assert
        var album = result.Value.ShouldNotBeNull().Album;
        album.Id.ShouldBeGreaterThan(0);
        album.Name.ShouldBe("Album");
        album.ArtistId.ShouldBe(artist.Id);
        album.Year.ShouldBe(1999);
    }

    [Fact]
    public async Task Create_ArtistAlreadyHasAlbumWithName_Returns409()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        scenario.CreateAlbum("Album", artist);
        var controller = CreateController(scenario);

        // Act
        var result = await controller.Create(new CreateAlbumRequest { Name = "Album", ArtistId = artist.Id },
            CreateAlbumCreateService(scenario), CancellationToken.None);

        // Assert
        var problem = result.Result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail
            .ShouldBe("Artist 'Artist' already has an album named 'Album'");
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var controller = CreateController(scenario);

        // Act
        var result = await controller.Create(new CreateAlbumRequest { Name = " ", ArtistId = artist.Id },
            CreateAlbumCreateService(scenario), CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }
}
