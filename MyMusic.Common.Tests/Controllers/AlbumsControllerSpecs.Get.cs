using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class AlbumsControllerGetSpecs
{
    private static AlbumsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        return new AlbumsController(
            Substitute.For<ILogger<AlbumsController>>(),
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
    public async Task Get_OwnedAlbum_Returns200()
    {
        // Arrange — current user owns the album (via a song)
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");
        var albumId = song.AlbumId;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(albumId, scenario.DbContext, CancellationToken.None);

        // Assert — owned album resolves normally
        response.Album.Id.ShouldBe(albumId);
        response.Album.Name.ShouldBe("My Song Album");
    }

    [Fact]
    public async Task Get_SharedAlbum_Returns200()
    {
        // Arrange — another user owns the album, but a song in it is shared with me (read access)
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(song, me, scenario.DbContext);
        var albumId = song.AlbumId;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(albumId, scenario.DbContext, CancellationToken.None);

        // Assert — recipient can open the album via direct link (shared-access gate)
        response.Album.Id.ShouldBe(albumId);
        response.Album.Name.ShouldBe("Shared Song Album");
    }

    [Fact]
    public async Task Get_NonSharedOtherOwnerAlbum_Returns404()
    {
        // Arrange — another user owns the album and no song in it is shared with me
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Private Other Song", ownerId: other.Id);
        var albumId = song.AlbumId;

        var controller = CreateController(scenario);

        // Act & Assert — access gate rejects the recipient; controller throws (404 in pipeline)
        await Should.ThrowAsync<Exception>(() =>
            controller.Get(albumId, scenario.DbContext, CancellationToken.None));
    }

    [Fact]
    public async Task Get_SharedAlbum_ReturnsOnlySharedSongs()
    {
        // Arrange — the owner's album holds two songs, but only one is shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var artist = scenario.CreateArtist("Other Artist", ownerId: other.Id);
        var album = scenario.CreateAlbum("Other Album", artist, ownerId: other.Id);
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id, album: album);
        var privateSong = scenario.CreateSong("Private Song", ownerId: other.Id, album: album);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(album.Id, scenario.DbContext, CancellationToken.None);

        // Assert — recipient sees the album, but only the shared song (the private one is trimmed)
        response.Album.Id.ShouldBe(album.Id);
        response.Album.Songs.ShouldHaveSingleItem();
        response.Album.Songs[0].Title.ShouldBe("Shared Song");
    }

    [Fact]
    public async Task Get_OwnedAlbum_SongIsSharedFalse()
    {
        // Arrange — current user owns the album; IsShared must be false on its songs
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");
        var albumId = song.AlbumId;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(albumId, scenario.DbContext, CancellationToken.None);

        // Assert — owned songs report IsShared = false (recipient import affordance is hidden)
        response.Album.Songs.ShouldHaveSingleItem();
        response.Album.Songs[0].IsShared.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_SharedAlbum_SongIsSharedTrue()
    {
        // Arrange — another user owns the album; a song in it is shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(song, me, scenario.DbContext);
        var albumId = song.AlbumId;

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(albumId, scenario.DbContext, CancellationToken.None);

        // Assert — recipient-visible songs report IsShared = true (import affordance shows)
        response.Album.Songs.ShouldHaveSingleItem();
        response.Album.Songs[0].IsShared.ShouldBeTrue();
    }
}