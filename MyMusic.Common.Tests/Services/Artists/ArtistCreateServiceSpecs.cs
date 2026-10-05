using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Artists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Artists;

public class ArtistCreateServiceSpecs
{
    private static ArtistCreateService CreateService(Scenario scenario) =>
        new(scenario.DbContext, scenario.AdvisoryLocks, Substitute.For<ILogger<ArtistCreateService>>());

    [Fact]
    public async Task CreateAsync_ValidName_CreatesArtistWithoutSongsOrAlbums()
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var artist = await CreateService(scenario).CreateAsync(scenario.AdminUser.Id, "  Artist  ");

        // Assert: the name is trimmed, and the artist is saved outside of any open transaction
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
        scenario.DbContext.ChangeTracker.Clear();
        var saved = scenario.DbContext.Artists.Single(a => a.Id == artist.Id);
        saved.Name.ShouldBe("Artist");
        saved.OwnerId.ShouldBe(scenario.AdminUser.Id);
        saved.SongsCount.ShouldBe(0);
        saved.AlbumsCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyName_ThrowsValidationException(string name)
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).CreateAsync(scenario.AdminUser.Id, name));

        // Assert
        exception.Message.ShouldBe("Artist name cannot be empty");
        scenario.DbContext.Artists.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_NameLongerThanTheColumn_ThrowsValidationException()
    {
        // Arrange
        var scenario = new Scenario();

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() =>
            CreateService(scenario).CreateAsync(scenario.AdminUser.Id, new string('a', 257)));
        scenario.DbContext.Artists.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_NameOfAnExistingArtist_CreatesAnotherArtist()
    {
        // Arrange: artist names are not unique
        var scenario = new Scenario();
        var existing = scenario.CreateArtist("Artist");

        // Act
        var artist = await CreateService(scenario).CreateAsync(scenario.AdminUser.Id, "Artist");

        // Assert
        artist.Id.ShouldNotBe(existing.Id);
        scenario.DbContext.Artists.Count(a => a.Name == "Artist").ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_TakesTheLockKeyOfASongImportOfThatArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var ownerId = scenario.AdminUser.Id;

        // Act
        await CreateService(scenario).CreateAsync(ownerId, " Artist ");

        // Assert
        var acquisition = scenario.AdvisoryLocks.Acquisitions.ShouldHaveSingleItem();
        acquisition.ShouldBe([AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Artist")]);
    }
}
