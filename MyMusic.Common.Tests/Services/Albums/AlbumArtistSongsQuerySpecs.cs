using MyMusic.Common.Services.Albums;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Albums;

public class AlbumArtistSongsQuerySpecs
{
    [Fact]
    public void OfAlbum_ReturnsOnlyTheSongsOfThatAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var otherAlbum = scenario.CreateAlbum("Other Album", artist);
        var first = scenario.CreateSong("First", album: album);
        var second = scenario.CreateSong("Second", album: album);
        scenario.CreateSong("Third", album: otherAlbum);

        // Act
        var songIds = AlbumArtistSongsQuery.OfAlbum(scenario.DbContext, scenario.AdminUser.Id, album.Id)
            .Select(s => s.Id)
            .ToList();

        // Assert
        songIds.ShouldBe([first.Id, second.Id], ignoreOrder: true);
    }

    [Fact]
    public void OfArtist_ReturnsSongsTheArtistPerformsAndSongsOfTheArtistsAlbums()
    {
        // Arrange
        var scenario = new Scenario();
        var artist = scenario.CreateArtist("Artist");
        var otherArtist = scenario.CreateArtist("Other Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var otherAlbum = scenario.CreateAlbum("Other Album", otherArtist);

        var ownSong = scenario.CreateSong("Own Song", album: album);
        var featuredSong = scenario.CreateSong("Featured", album: otherAlbum, artists: [otherArtist, artist]);
        var albumOnlySong = scenario.CreateSong("Album Only", album: album, artists: [otherArtist]);
        scenario.CreateSong("Unrelated", album: otherAlbum);

        // Act
        var songs = AlbumArtistSongsQuery.OfArtist(scenario.DbContext, scenario.AdminUser.Id, artist.Id);

        // Assert: a song both performed by the artist and in one of their albums is counted once
        songs.Select(s => s.Id).ToList()
            .ShouldBe([ownSong.Id, featuredSong.Id, albumOnlySong.Id], ignoreOrder: true);
        songs.Count().ShouldBe(3);
    }

    [Fact]
    public void OfAlbumAndOfArtist_AnotherOwner_ReturnNothing()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        scenario.CreateSong("Song", album: album);

        // Act & Assert
        AlbumArtistSongsQuery.OfAlbum(scenario.DbContext, otherUser.Id, album.Id).ShouldBeEmpty();
        AlbumArtistSongsQuery.OfArtist(scenario.DbContext, otherUser.Id, artist.Id).ShouldBeEmpty();
    }
}
