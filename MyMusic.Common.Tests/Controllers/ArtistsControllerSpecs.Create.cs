using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Artists;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Artists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class ArtistsControllerCreateSpecs
{
    private static ArtistsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new ArtistsController(Substitute.For<ILogger<ArtistsController>>(), currentUser);
    }

    private static ArtistCreateService CreateArtistCreateService(Scenario scenario) =>
        new(scenario.DbContext, scenario.AdvisoryLocks, Substitute.For<ILogger<ArtistCreateService>>());

    [Fact]
    public async Task Create_ValidRequest_ReturnsTheCreatedArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act
        var result = await controller.Create(new CreateArtistRequest { Name = "Artist" },
            CreateArtistCreateService(scenario), CancellationToken.None);

        // Assert
        var artist = result.Value.ShouldNotBeNull().Artist;
        artist.Id.ShouldBeGreaterThan(0);
        artist.Name.ShouldBe("Artist");
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Act
        var result = await controller.Create(new CreateArtistRequest { Name = " " },
            CreateArtistCreateService(scenario), CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }
}
