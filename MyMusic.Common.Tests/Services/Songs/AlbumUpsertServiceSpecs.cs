using MyMusic.Common.Entities;
using MyMusic.Common.Services.Songs;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class AlbumUpsertServiceSpecs
{
    [Fact]
    public async Task UpsertAsync_ArtistHasAlbumWithName_ReturnsIt()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        scenario.DbContext.ChangeTracker.Clear();
        var trackedArtist = scenario.DbContext.Artists.First(a => a.Id == artist.Id);

        // Act
        var result = await new AlbumUpsertService().UpsertAsync(scenario.DbContext, scenario.AdminUser.Id, "Album",
            trackedArtist);

        // Assert
        result.Id.ShouldBe(album.Id);
        result.Artist.ShouldBeSameAs(trackedArtist);
    }

    [Fact]
    public async Task UpsertAsync_OnlyAnotherArtistHasAlbumWithName_CreatesAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var otherAlbum = scenario.CreateAlbum("Album", scenario.CreateArtist("Other Artist"));
        var artist = scenario.CreateArtist("Artist");

        // Act
        var result = await new AlbumUpsertService().UpsertAsync(scenario.DbContext, scenario.AdminUser.Id, "Album",
            artist);
        await scenario.DbContext.SaveChangesAsync();

        // Assert
        result.Id.ShouldNotBe(otherAlbum.Id);
        result.ArtistId.ShouldBe(artist.Id);
        scenario.DbContext.Albums.Count(a => a.Name == "Album").ShouldBe(2);
    }

    [Fact]
    public async Task UpsertAsync_UnsavedArtist_CreatesAlbumOnce()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = new Artist { Name = "Artist", OwnerId = scenario.AdminUser.Id, CreatedAt = DateTime.UtcNow };
        scenario.DbContext.Add(artist);
        var service = new AlbumUpsertService();

        // Act
        var first = await service.UpsertAsync(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        var second = await service.UpsertAsync(scenario.DbContext, scenario.AdminUser.Id, "Album", artist);
        await scenario.DbContext.SaveChangesAsync();

        // Assert
        second.ShouldBeSameAs(first);
        scenario.DbContext.Albums.Count(a => a.Name == "Album").ShouldBe(1);
    }
}
