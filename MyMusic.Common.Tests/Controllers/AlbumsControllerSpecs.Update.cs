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

public class AlbumsControllerUpdateSpecs
{
    private static AlbumsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new AlbumsController(Substitute.For<ILogger<AlbumsController>>(), currentUser);
    }

    private static AlbumEditService CreateAlbumEditService(Scenario scenario) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(),
            Substitute.For<ILogger<AlbumEditService>>());

    private static Task<IActionResult> Update(Scenario scenario, params UpdateAlbumsItem[] albums) =>
        CreateController(scenario).Update(new UpdateAlbumsRequest { Albums = albums.ToList() },
            CreateAlbumEditService(scenario), CancellationToken.None);

    [Fact]
    public async Task Update_SeveralAlbums_Returns204AndSavesThemAll()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var first = scenario.CreateAlbum("First", artist);
        var second = scenario.CreateAlbum("Second", artist);

        // Act
        var result = await Update(scenario,
            new UpdateAlbumsItem { Id = first.Id, Name = "First (Renamed)", Year = 1999 },
            new UpdateAlbumsItem { Id = second.Id, Name = "Second", Year = 2000 });

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        scenario.DbContext.ChangeTracker.Clear();
        var albums = scenario.DbContext.Albums.OrderBy(a => a.Id).ToList();
        albums.Select(a => a.Name).ShouldBe(["First (Renamed)", "Second"]);
        albums.Select(a => a.Year).ShouldBe([1999, 2000]);
    }

    [Fact]
    public async Task Update_AlbumOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var result = await Update(scenario, new UpdateAlbumsItem { Id = othersAlbum.Id, Name = "Renamed" });

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        scenario.DbContext.Albums.Single().Name.ShouldBe("Album");
    }

    [Fact]
    public async Task Update_NameOfAnotherAlbumOfTheSameArtist_Returns409PointingToMerge()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        scenario.CreateAlbum("Taken", artist);

        // Act
        var result = await Update(scenario, new UpdateAlbumsItem { Id = album.Id, Name = "Taken" });

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldBe(
            "Artist 'Artist' already has an album named 'Taken'. To join the two albums, merge them instead.");
    }

    [Fact]
    public async Task Update_EmptyName_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));

        // Act
        var result = await Update(scenario, new UpdateAlbumsItem { Id = album.Id, Name = " " });

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_RenamedPlaceholderAlbum_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, scenario.CreateArtist("Artist"));

        // Act
        var result = await Update(scenario, new UpdateAlbumsItem { Id = placeholder.Id, Name = "Renamed" });

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldContain("cannot be renamed");
    }
}
