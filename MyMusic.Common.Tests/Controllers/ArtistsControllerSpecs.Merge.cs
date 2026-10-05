using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Common.Services.Artists;
using MyMusic.Common.Tests.Utilities;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Artists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class ArtistsControllerMergeSpecs
{
    private static ArtistsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new ArtistsController(Substitute.For<ILogger<ArtistsController>>(), currentUser);
    }

    private static ArtistMergeService CreateArtistMergeService(Scenario scenario)
    {
        var songUpdate = scenario.CreateSongUpdateService();
        var albumDelete = new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>());
        var artistDelete = new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>());
        var artworkDelete = new ArtworkDeleteService(scenario.DbContext,
            Substitute.For<ILogger<ArtworkDeleteService>>());

        return new ArtistMergeService(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            songUpdate,
            new AlbumMergeService(scenario.DbContext, scenario.FileTransactions, scenario.AdvisoryLocks, songUpdate,
                albumDelete, artistDelete, artworkDelete, Substitute.For<ILogger<AlbumMergeService>>()),
            artistDelete,
            artworkDelete,
            Substitute.For<ILogger<ArtistMergeService>>());
    }

    private static Task<IActionResult> Merge(Scenario scenario, long targetId, params long[] sourceIds) =>
        CreateController(scenario).Merge(
            new MergeArtistsRequest { TargetId = targetId, SourceIds = sourceIds.ToList() },
            CreateArtistMergeService(scenario), CancellationToken.None);

    private static Task<ActionResult<PreviewArtistsMergeResponse>> PreviewMerge(Scenario scenario, long targetId,
        params long[] sourceIds) =>
        CreateController(scenario).PreviewMerge(
            new PreviewArtistsMergeRequest { TargetId = targetId, SourceIds = sourceIds.ToList() },
            CreateArtistMergeService(scenario), CancellationToken.None);

    [Fact]
    public async Task Merge_SeveralArtists_Returns204AndKeepsOnlyTheTarget()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");

        // Act
        var result = await Merge(scenario, target.Id, first.Id, second.Id);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Artists.Select(a => a.Id).ToList().ShouldBe([target.Id]);
    }

    [Fact]
    public async Task Merge_ArtistOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateArtist("Target");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var result = await Merge(scenario, target.Id, othersArtist.Id);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        scenario.DbContext.Artists.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Merge_PlaceholderArtistIntoAnother_Returns400WithTheReason()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);

        // Act
        var result = await Merge(scenario, target.Id, placeholder.Id);

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull()
            .ShouldContain("cannot be merged into another artist");
    }

    [Fact]
    public async Task Merge_TargetAmongTheArtistsToMerge_Returns400()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");

        // Act
        var result = await Merge(scenario, target.Id, target.Id);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PreviewMerge_ArtistSharingAnAlbumNameWithTheTarget_ReturnsTheSongsAndMergedAlbums()
    {
        // Arrange
        var scenario = new Scenario();
        var target = scenario.CreateArtist("Target");
        var source = scenario.CreateArtist("Source");
        scenario.CreateAlbum("Album", target);
        var album = scenario.CreateAlbum("Album", source);
        scenario.CreateSong("First", album: album);
        scenario.CreateSong("Second", album: album);

        // Act
        var result = await PreviewMerge(scenario, target.Id, source.Id);

        // Assert
        var preview = result.Value.ShouldNotBeNull();
        preview.SongsCount.ShouldBe(2);
        preview.MergedAlbumsCount.ShouldBe(1);
    }

    [Fact]
    public async Task PreviewMerge_ArtistOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var target = scenario.CreateArtist("Target");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var result = await PreviewMerge(scenario, target.Id, othersArtist.Id);

        // Assert
        result.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}
