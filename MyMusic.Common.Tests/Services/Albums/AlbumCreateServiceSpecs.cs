using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Albums;

public class AlbumCreateServiceSpecs
{
    private static AlbumCreateService CreateService(Scenario scenario) =>
        new(scenario.DbContext, scenario.AdvisoryLocks, Substitute.For<ILogger<AlbumCreateService>>());

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesEmptyAlbumOfTheArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var album = await CreateService(scenario).CreateAsync(scenario.AdminUser.Id,
            new AlbumCreateInput { Name = "  Album  ", ArtistId = artist.Id, Year = 2001 });

        // Assert: the name is trimmed, and the album is saved outside of any open transaction
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        var saved = scenario.DbContext.Albums.Single(a => a.Id == album.Id);
        saved.Name.ShouldBe("Album");
        saved.ArtistId.ShouldBe(artist.Id);
        saved.OwnerId.ShouldBe(scenario.AdminUser.Id);
        saved.Year.ShouldBe(2001);
        saved.SongsCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyName_ThrowsValidationException(string name)
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() => CreateService(scenario)
            .CreateAsync(scenario.AdminUser.Id, new AlbumCreateInput { Name = name, ArtistId = artist.Id }));

        // Assert
        exception.Message.ShouldBe("Album name cannot be empty");
        scenario.DbContext.Albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_NameLongerThanTheColumn_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() => CreateService(scenario).CreateAsync(scenario.AdminUser.Id,
            new AlbumCreateInput { Name = new string('a', 257), ArtistId = artist.Id }));
        scenario.DbContext.Albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ArtistAlreadyHasAlbumWithName_ThrowsAlbumAlreadyExistsException()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        scenario.CreateAlbum("Album", artist);

        // Act: the trimmed name is the one compared
        var exception = await Should.ThrowAsync<AlbumAlreadyExistsException>(() => CreateService(scenario)
            .CreateAsync(scenario.AdminUser.Id, new AlbumCreateInput { Name = " Album ", ArtistId = artist.Id }));

        // Assert
        exception.Message.ShouldBe("Artist 'Artist' already has an album named 'Album'");
        scenario.DbContext.Albums.Count().ShouldBe(1);
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_OnlyAnotherArtistHasAlbumWithName_CreatesAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var otherAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Other Artist"));
        var artist = scenario.CreateArtist("Artist");

        // Act
        var album = await CreateService(scenario).CreateAsync(scenario.AdminUser.Id,
            new AlbumCreateInput { Name = "Album", ArtistId = artist.Id });

        // Assert
        album.Id.ShouldNotBe(otherAlbum.Id);
        album.ArtistId.ShouldBe(artist.Id);
        scenario.DbContext.Albums.Count(a => a.Name == "Album").ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_ArtistOfAnotherOwner_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var othersArtist = scenario.CreateArtist("Artist", other.Id);

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() => CreateService(scenario)
            .CreateAsync(scenario.AdminUser.Id, new AlbumCreateInput { Name = "Album", ArtistId = othersArtist.Id }));

        // Assert
        exception.Message.ShouldBe($"Artist not found with id {othersArtist.Id}");
        scenario.DbContext.Albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_TakesTheLockKeysOfASongImportOfThatAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var ownerId = scenario.AdminUser.Id;
        var artist = scenario.CreateArtist("Artist");

        // Act
        await CreateService(scenario).CreateAsync(ownerId,
            new AlbumCreateInput { Name = " Album ", ArtistId = artist.Id });

        // Assert: one acquisition, with the artist key and the (artist, trimmed album name) key
        var acquisition = scenario.AdvisoryLocks.Acquisitions.ShouldHaveSingleItem();
        acquisition.ShouldBe(AdvisoryLockKey.Normalize(
        [
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Artist"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist", "Album"),
        ]));
    }

    [Fact]
    public async Task CreateAsync_ReleasesItsLocks_SoTheSameNameCanBeLockedAgain()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var service = CreateService(scenario);
        await service.CreateAsync(scenario.AdminUser.Id, new AlbumCreateInput { Name = "Album", ArtistId = artist.Id });

        // Act: a second attempt needs the same keys, and would hang if the first one still held them
        var second = service.CreateAsync(scenario.AdminUser.Id,
            new AlbumCreateInput { Name = "Album", ArtistId = artist.Id });

        // Assert
        await Should.ThrowAsync<AlbumAlreadyExistsException>(() => second.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
