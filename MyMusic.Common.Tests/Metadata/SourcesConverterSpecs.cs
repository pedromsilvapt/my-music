using MyMusic.Common.Metadata;
using MyMusic.Common.Sources;
using Shouldly;

namespace MyMusic.Common.Tests.Metadata;

public class SourcesConverterSpecs
{
    private static readonly SourceSongArtist Artist = new() { Id = "a1", Name = "Source Artist" };

    [Fact]
    public void ToSong_WithAlbum_MapsAlbumAndArtist()
    {
        // Arrange
        var song = new SourceSong
        {
            Id = "s1",
            Title = "Source Title",
            Album = new SourceSongAlbum { Id = "al1", Name = "Source Album", Artist = Artist },
            Artists = [Artist],
            Genres = [],
        };

        // Act
        var metadata = SourcesConverter.ToSong(song);

        // Assert
        metadata.Album.ShouldNotBeNull();
        metadata.Album.Name.ShouldBe("Source Album");
        metadata.Album.Artist.ShouldNotBeNull();
        metadata.Album.Artist.Name.ShouldBe("Source Artist");
    }

    [Fact]
    public void ToSong_WithoutAlbum_ReturnsMetadataWithoutAlbum()
    {
        // Arrange
        var song = new SourceSong
        {
            Id = "s1",
            Title = "Source Title",
            Album = null,
            Artists = [Artist],
            Genres = [],
        };

        // Act
        var metadata = SourcesConverter.ToSong(song);

        // Assert
        metadata.Album.ShouldBeNull();
        metadata.Title.ShouldBe("Source Title");
        metadata.Artists.ShouldNotBeNull().ShouldHaveSingleItem().Name.ShouldBe("Source Artist");
    }
}
