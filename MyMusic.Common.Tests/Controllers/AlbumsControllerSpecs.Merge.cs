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

public class AlbumsControllerMergeSpecs
{
    private static AlbumsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new AlbumsController(Substitute.For<ILogger<AlbumsController>>(), currentUser);
    }

    private static AlbumMergeService CreateAlbumMergeService(Scenario scenario) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<AlbumMergeService>>());

    private static Task<IActionResult> Merge(Scenario scenario, long targetId, params long[] sourceIds) =>
        CreateController(scenario).Merge(
            new MergeAlbumsRequest { TargetId = targetId, SourceIds = sourceIds.ToList() },
            CreateAlbumMergeService(scenario), CancellationToken.None);

    private static Task<ActionResult<PreviewAlbumsMergeResponse>> PreviewMerge(Scenario scenario, long targetId,
        params long[] sourceIds) =>
        CreateController(scenario).PreviewMerge(
            new PreviewAlbumsMergeRequest { TargetId = targetId, SourceIds = sourceIds.ToList() },
            CreateAlbumMergeService(scenario), CancellationToken.None);

    [Fact]
    public async Task Merge_SeveralAlbums_Returns204AndKeepsOnlyTheTarget()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var first = scenario.CreateAlbum("First", artist);
        var second = scenario.CreateAlbum("Second", artist);

        // Act
        var result = await Merge(scenario, target.Id, first.Id, second.Id);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Select(a => a.Id).ToList().ShouldBe([target.Id]);
    }

    [Fact]
    public async Task Merge_AlbumOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateAlbum("Target", scenario.CreateArtist("Artist"));
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var result = await Merge(scenario, target.Id, othersAlbum.Id);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        scenario.DbContext.Albums.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Merge_PlaceholderAlbumIntoAnother_Returns400WithTheReason()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var target = scenario.CreateAlbum("Target", artist);
        var placeholder = scenario.CreateAlbum(Album.PlaceholderName, artist);

        // Act
        var result = await Merge(scenario, target.Id, placeholder.Id);

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull()
            .ShouldContain("cannot be merged into another album");
    }

    [Fact]
    public async Task Merge_TargetAmongTheAlbumsToMerge_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateAlbum("Target", scenario.CreateArtist("Artist"));

        // Act
        var result = await Merge(scenario, target.Id, target.Id);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PreviewMerge_AlbumOfAnotherArtist_ReturnsTheSongsThatMoveAndGainTheArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateAlbum("Target", scenario.CreateArtist("Target Artist"));
        var source = scenario.CreateAlbum("Source", scenario.CreateArtist("Source Artist"));
        scenario.CreateSong("First", album: source);
        scenario.CreateSong("Second", album: source);

        // Act
        var result = await PreviewMerge(scenario, target.Id, source.Id);

        // Assert
        var preview = result.Value.ShouldNotBeNull();
        preview.SongsCount.ShouldBe(2);
        preview.SongsGainingArtistCount.ShouldBe(2);
        preview.TargetArtistName.ShouldBe("Target Artist");
    }

    [Fact]
    public async Task PreviewMerge_AlbumOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateAlbum("Target", scenario.CreateArtist("Artist"));
        var othersAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist", other.Id), other.Id);

        // Act
        var result = await PreviewMerge(scenario, target.Id, othersAlbum.Id);

        // Assert
        result.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}
