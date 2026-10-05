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

public class ArtistsControllerDeleteSpecs
{
    private static ArtistsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new ArtistsController(Substitute.For<ILogger<ArtistsController>>(), currentUser);
    }

    private static ArtistRemoveService CreateArtistRemoveService(Scenario scenario) =>
        new(scenario.DbContext,
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            scenario.CreateSongUpdateService(),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<ArtistRemoveService>>());

    [Fact]
    public async Task GetUsage_ArtistWithOwnAndFeaturedSongs_CountsEachAffectedSongOnce()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var other = scenario.CreateArtist("Other");
        var album = scenario.CreateAlbum("Album", artist);
        var otherAlbum = scenario.CreateAlbum("Other Album", other);
        scenario.CreateSong("Own", album: album);
        scenario.CreateSong("Featured", album: otherAlbum, artists: [other, artist]);
        scenario.CreateSong("Unrelated", album: otherAlbum);

        // Act
        var result = await CreateController(scenario).GetUsage(new GetArtistsUsageRequest { ArtistIds = [artist.Id] }, scenario.DbContext, CancellationToken.None);

        // Assert
        result.SongsCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetUsage_SeveralArtists_CountsASongTheyShareOnceAndOwnedSongsOnly()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var first = scenario.CreateArtist("First");
        var second = scenario.CreateArtist("Second");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);
        scenario.CreateSong("Duet", album: scenario.CreateAlbum("Album", first), artists: [first, second]);
        scenario.CreateSong("Solo", album: scenario.CreateAlbum("Solo Album", second));
        scenario.CreateSong("Theirs", ownerId: other.Id, album: scenario.CreateAlbum("Album", othersArtist, other.Id));

        // Act
        var result = await CreateController(scenario).GetUsage(
            new GetArtistsUsageRequest { ArtistIds = [first.Id, second.Id, othersArtist.Id] }, scenario.DbContext,
            CancellationToken.None);

        // Assert
        result.SongsCount.ShouldBe(2);
    }

    [Fact]
    public async Task Delete_ArtistWithoutSongs_Returns204AndDeletesIt()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var result = await CreateController(scenario)
            .Delete(new DeleteArtistsRequest { ArtistIds = [artist.Id] }, CreateArtistRemoveService(scenario), CancellationToken.None);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        scenario.DbContext.Artists.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_ArtistOfAnotherOwner_Returns404()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var result = await CreateController(scenario)
            .Delete(new DeleteArtistsRequest { ArtistIds = [othersArtist.Id] }, CreateArtistRemoveService(scenario), CancellationToken.None);

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        scenario.DbContext.Artists.Any(a => a.Id == othersArtist.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_PlaceholderArtistWithSongs_Returns409()
    {
        // Arrange
        var scenario = new Scenario();
        var placeholder = scenario.CreateArtist(Artist.PlaceholderName);
        scenario.CreateSong("Song", album: scenario.CreateAlbum(Album.PlaceholderName, placeholder));

        // Act
        var result = await CreateController(scenario)
            .Delete(new DeleteArtistsRequest { ArtistIds = [placeholder.Id] }, CreateArtistRemoveService(scenario), CancellationToken.None);

        // Assert
        var problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull()
            .ShouldContain("cannot be deleted while it still has songs");
    }
}
