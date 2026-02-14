using MyMusic.Common.Sources;

namespace MyMusic.Common.Metadata;

public static class SourcesConverter
{
    #region To Metadata

    public static SongMetadata ToSong(SourceSong song)
    {
        var album = ToAlbum(song.Album, song.Cover);
        var artists = song.Artists.Select(ToArtist).ToList();

        return new SongMetadata(song.Id, song.Title)
        {
            Explicit = song.Explicit,
            Artists = artists,
            Genres = song.Genres.ToList(),
            Album = album,
            Year = song.Year,
            Lyrics = song.Lyrics,
            Duration = song.Duration,
            Rating = null,
            Track = song.Track,
        };
    }

    public static AlbumMetadata ToAlbum(SourceSongAlbum album, SourceArtwork? coverArt)
    {
        return new AlbumMetadata(album.Id, album.Name, ToCoverArt(coverArt))
        {
            Artist = ToArtist(album.Artist),
        };
    }

    public static CoverArtMetadata ToCoverArt(SourceArtwork? artwork)
    {
        return new CoverArtMetadata
        {
            Small = artwork?.Small,
            Normal = artwork?.Normal,
            Big = artwork?.Big,
        };
    }

    public static ArtistMetadata ToArtist(SourceSongArtist artist)
    {
        return new ArtistMetadata(artist.Id, artist.Name);
    }

    #endregion To Metadata

    #region From Metadata

    // TODO

    #endregion From Metadata

    #region Utility Methods

    #endregion Utility Methods
}