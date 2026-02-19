using MyMusic.Common.Entities;

namespace MyMusic.Common.Metadata;

public static class EntityConverter
{
    #region To Metadata

    public static SongMetadata ToSong(Song song)
    {
        return new SongMetadata(song.Id.ToString(), song.Title)
        {
            Artists = song.Artists?.Select(sa => ToArtist(sa.Artist))?.ToList(),
            Album = ToAlbum(song, song.Album),
            Genres = song.Genres?.Select(g => g.Genre.Name).ToList() ?? [],
            Explicit = song.Explicit,
            Duration = song.Duration,
            Track = song.Track,
            Rating = song.Rating,
            Year = song.Year,
            Lyrics = song.Lyrics,
        };
    }

    public static AlbumMetadata ToAlbum(Song song, Album? musicAlbum)
    {
        var coverArt = ToCoverArt(song.Cover);

        var album = new AlbumMetadata(null, "", coverArt);

        if (musicAlbum is null)
        {
            return album;
        }

        album.Id = musicAlbum.Id.ToString();
        album.Name = musicAlbum.Name;

        album.Artist = ToArtist(musicAlbum.Artist);

        return album;
    }

    public static ArtistMetadata ToArtist(Artist musicArtist)
    {
        return new ArtistMetadata(musicArtist.Id.ToString(), musicArtist.Name);
    }

    public static CoverArtMetadata ToCoverArt(Artwork? cover)
    {
        var coverArt = new CoverArtMetadata();

        if (cover is not null)
        {
            coverArt.Big = ImageBuffer.FromBytes(cover.Data, cover.MimeType).ToBase64Url();
        }

        return coverArt;
    }

    #endregion To Metadata

    #region From Metadata

    // TODO

    #endregion From Metadata
}