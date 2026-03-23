using MyMusic.Server.DTO.Songs;
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
    public required List<GetArtistSongItem> Songs { get; set; }

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
            Songs = songs.Select(GetArtistSongItem.FromEntity).ToList(),
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

public record GetArtistSongItem
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Title { get; set; }
    public required List<GetArtistSongItemArtist> Artists { get; set; }
    public required GetArtistSongItemAlbum Album { get; set; }
    public required List<GetArtistResponseGenre> Genres { get; set; }
    public required int? Year { get; set; }
    public required string Duration { get; set; }
    public required List<ListSongsDevice> Devices { get; set; }
    public required bool IsFavorite { get; set; }
    public required bool IsExplicit { get; set; }

    public static GetArtistSongItem FromEntity(Entities.SongArtist songArtist)
    {
        var song = songArtist.Song;
        return new GetArtistSongItem
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = song.Artists.Select(GetArtistSongItemArtist.FromEntity).ToList(),
            Album = GetArtistSongItemAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(GetArtistResponseGenre.FromEntity).ToList(),
            Year = song.Year,
            Duration = song.Duration.ToString(@"mm\:ss"),
            Devices = song.Devices.Select(d => ListSongsDevice.FromEntity(d.Device)).ToList(),
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

public record GetArtistSongItemArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetArtistSongItemArtist FromEntity(Entities.SongArtist songArtist)
    {
        return new GetArtistSongItemArtist
        {
            Id = songArtist.ArtistId,
            Name = songArtist.Artist.Name,
        };
    }
}

public record GetArtistSongItemAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetArtistSongItemAlbum FromEntity(Entities.Album album)
    {
        return new GetArtistSongItemAlbum
        {
            Id = album.Id,
            Name = album.Name,
        };
    }
}