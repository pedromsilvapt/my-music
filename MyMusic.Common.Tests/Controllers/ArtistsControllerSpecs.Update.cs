using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Artists;
using MyMusic.Common.Tests.Utilities;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Artists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class ArtistsControllerUpdateSpecs
{
    private static ArtistsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new ArtistsController(Substitute.For<ILogger<ArtistsController>>(), currentUser);
    }

    private static ArtistEditService CreateArtistEditService(Scenario scenario) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(),
            Substitute.For<ILogger<ArtistEditService>>());

    private static Task<IActionResult> Update(Scenario scenario, params UpdateArtistsItem[] artists) =>
        CreateController(scenario).Update(new UpdateArtistsRequest { Artists = artists.ToList() },
            CreateArtistEditService(scenario), CancellationToken.None);

    [Fact]
    public async Task Update_SeveralArtists_Returns204AndRenamesThemAll()
    {
        // Arrange
        var scenario = new Scenario();
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");

        // Act
        var result = await Update(scenario,
            new UpdateArtistsItem { Id = first.Id, Name = "First (Renamed)" },
            new UpdateArtistsItem { Id = second.Id, Name = "Second (Renamed)" });

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.OrderBy(a => a.Id).Select(a => a.Name).ToList()
            .ShouldBe(["First (Renamed)", "Second (Renamed)"]);
    }

    [Fact]
    public async Task Update_ArtistOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var result = await Update(scenario, new UpdateArtistsItem { Id = othersArtist.Id, Name = "Renamed" });

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        scenario.DbContext.Artists.Single().Name.ShouldBe("Artist");
    }

    [Fact]
    public async Task Update_EmptyName_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var result = await Update(scenario, new UpdateArtistsItem { Id = artist.Id, Name = " " });

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_RenamedPlaceholderArtist_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);

        // Act
        var result = await Update(scenario, new UpdateArtistsItem { Id = placeholder.Id, Name = "Renamed" });

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldContain("cannot be renamed");
    }
}
