using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Artists;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class ArtistsControllerGetSpecs
{
    private static ArtistsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        return new ArtistsController(
            Substitute.For<ILogger<ArtistsController>>(),
            currentUser);
    }

    private static SongSharing Share(Song song, User recipient, MusicDbContext db)
    {
        var sharing = new SongSharing
        {
            SongId = song.Id,
            UserId = recipient.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.SongSharings.Add(sharing);
        db.SaveChanges();
        return sharing;
    }

    [Fact]
    public async Task Get_OwnedArtist_Returns200()
    {
        // Arrange — current user owns the artist (created via CreateSong)
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");
        var artistId = scenario.DbContext.Artists.First(a => a.OwnerId == scenario.AdminUser.Id).Id;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(artistId, ArtistSongFilter.All, scenario.DbContext, CancellationToken.None);

        // Assert — owned artist resolves normally
        response.Artist.Id.ShouldBe(artistId);
        response.Artist.Name.ShouldBe("My Song Artist");
    }

    [Fact]
    public async Task Get_SharedArtist_Returns200()
    {
        // Arrange — another user owns the artist; a song linked to it is shared with me (read access)
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(song, me, scenario.DbContext);
        var artistId = song.Album.ArtistId;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(artistId, ArtistSongFilter.All, scenario.DbContext, CancellationToken.None);

        // Assert — recipient can open the artist via direct link (shared-access gate)
        response.Artist.Id.ShouldBe(artistId);
        response.Artist.Name.ShouldBe("Shared Song Artist");
    }

    [Fact]
    public async Task Get_NonSharedOtherOwnerArtist_Returns404()
    {
        // Arrange — another user owns the artist and no linked song is shared with me
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Private Other Song", ownerId: other.Id);
        var artistId = song.Album.ArtistId;

        var controller = CreateController(scenario);

        // Act & Assert — access gate rejects the recipient; controller throws (404 in pipeline)
        await Should.ThrowAsync<Exception>(() =>
            controller.Get(artistId, ArtistSongFilter.All, scenario.DbContext, CancellationToken.None));
    }

    [Fact]
    public async Task Get_SharedArtist_ReturnsOnlySharedSongsAndAlbums()
    {
        // Arrange — the owner's artist has two albums/songs; only one song (and its album) is shared
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var artist = scenario.CreateArtist("Other Artist", ownerId: other.Id);
        var sharedAlbum = scenario.CreateAlbum("Shared Album", artist, ownerId: other.Id);
        var privateAlbum = scenario.CreateAlbum("Private Album", artist, ownerId: other.Id);
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id, album: sharedAlbum);
        var privateSong = scenario.CreateSong("Private Song", ownerId: other.Id, album: privateAlbum);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(artist.Id, ArtistSongFilter.All, scenario.DbContext, CancellationToken.None);

        // Assert — recipient sees the artist, but only the shared song and its album (the private ones are trimmed)
        response.Artist.Id.ShouldBe(artist.Id);
        response.Artist.Songs.ShouldHaveSingleItem();
        response.Artist.Songs[0].Title.ShouldBe("Shared Song");
        response.Artist.Albums.ShouldHaveSingleItem();
        response.Artist.Albums[0].Name.ShouldBe("Shared Album");
    }

    [Fact]
    public async Task Get_OwnedArtist_SongIsSharedFalse()
    {
        // Arrange — current user owns the artist; IsShared must be false on its songs
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");
        var artistId = scenario.DbContext.Artists.First(a => a.OwnerId == scenario.AdminUser.Id).Id;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(artistId, ArtistSongFilter.All, scenario.DbContext, CancellationToken.None);

        // Assert — owned songs report IsShared = false (recipient import affordance is hidden)
        response.Artist.Songs.ShouldHaveSingleItem();
        response.Artist.Songs[0].IsShared.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_SharedArtist_SongIsSharedTrue()
    {
        // Arrange — another user owns the artist; a song linked to it is shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(song, me, scenario.DbContext);
        var artistId = song.Album.ArtistId;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(artistId, ArtistSongFilter.All, scenario.DbContext, CancellationToken.None);

        // Assert — recipient-visible songs report IsShared = true (import affordance shows)
        response.Artist.Songs.ShouldHaveSingleItem();
        response.Artist.Songs[0].IsShared.ShouldBeTrue();
    }
}