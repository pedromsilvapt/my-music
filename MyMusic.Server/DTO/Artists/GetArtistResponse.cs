using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Artists;

public enum ArtistSongFilter
{
    All,
    Own,
    Other,
}

public record GetArtistResponse
{
    public required GetArtistResponseArtist Artist { get; set; }
}

public record GetArtistResponseArtist
{
    public required long Id { get; set; }
    public required long? Photo { get; set; }
    public required string Name { get; set; }
    public required int AlbumsCount { get; set; }
    public required int SongsCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required List<GetArtistResponseAlbum> Albums { get; set; }
    public required List<GetArtistResponseSong> Songs { get; set; }

    public static GetArtistResponseArtist FromEntity(Entities.Artist artist, ArtistSongFilter songFilter)
    {
        var songs = songFilter switch
        {
            ArtistSongFilter.Own => artist.Songs.Where(sa => sa.Song.Album.ArtistId == artist.Id).ToList(),
            ArtistSongFilter.Other => artist.Songs.Where(sa => sa.Song.Album.ArtistId != artist.Id).ToList(),
            _ => artist.Songs.ToList(),
        };

        return new GetArtistResponseArtist
        {
            Id = artist.Id,
            Photo = artist.PhotoId,
            Name = artist.Name,
            AlbumsCount = artist.AlbumsCount,
            SongsCount = artist.SongsCount,
            CreatedAt = artist.CreatedAt,
            Albums = artist.Albums.Select(GetArtistResponseAlbum.FromEntity).ToList(),
            Songs = songs.Select(GetArtistResponseSong.FromEntity).ToList(),
        };
    }
}

public record GetArtistResponseAlbum
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Name { get; set; }
    public required int? Year { get; set; }
    public required int SongsCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required GetArtistResponseAlbumArtist Artist { get; set; }

    public static GetArtistResponseAlbum FromEntity(Entities.Album album)
    {
        return new GetArtistResponseAlbum
        {
            Id = album.Id,
            Cover = album.CoverId,
            Name = album.Name,
            Year = album.Year,
            SongsCount = album.SongsCount,
            CreatedAt = album.CreatedAt,
            Artist = GetArtistResponseAlbumArtist.FromEntity(album.Artist),
        };
    }
}

public record GetArtistResponseAlbumArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetArtistResponseAlbumArtist FromEntity(Entities.Artist artist)
    {
        return new GetArtistResponseAlbumArtist
        {
            Id = artist.Id,
            Name = artist.Name,
        };
    }
}

public record GetArtistResponseSong
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Title { get; set; }
    public required List<GetArtistResponseSongArtist> Artists { get; set; }
    public required GetArtistResponseSongAlbum Album { get; set; }
    public required List<GetArtistResponseGenre> Genres { get; set; }
    public required int? Year { get; set; }
    public required string Duration { get; set; }
    public required bool IsFavorite { get; set; }
    public required bool IsExplicit { get; set; }

    public static GetArtistResponseSong FromEntity(Entities.SongArtist songArtist)
    {
        var song = songArtist.Song;
        return new GetArtistResponseSong
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = song.Artists.Select(GetArtistResponseSongArtist.FromEntity).ToList(),
            Album = GetArtistResponseSongAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(GetArtistResponseGenre.FromEntity).ToList(),
            Year = song.Year,
            Duration = song.Duration.ToString(@"mm\:ss"),
            IsFavorite = false,
            IsExplicit = song.Explicit,
        };
    }
}

public record GetArtistResponseGenre
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetArtistResponseGenre FromEntity(Entities.SongGenre songGenre)
    {
        return new GetArtistResponseGenre
        {
            Id = songGenre.GenreId,
            Name = songGenre.Genre.Name,
        };
    }
}

public record GetArtistResponseSongArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetArtistResponseSongArtist FromEntity(Entities.SongArtist songArtist)
    {
        return new GetArtistResponseSongArtist
        {
            Id = songArtist.ArtistId,
            Name = songArtist.Artist.Name,
        };
    }
}

public record GetArtistResponseSongAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetArtistResponseSongAlbum FromEntity(Entities.Album album)
    {
        return new GetArtistResponseSongAlbum
        {
            Id = album.Id,
            Name = album.Name,
        };
    }
}