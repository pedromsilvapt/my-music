using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Albums;

public record GetAlbumResponse
{
    public required GetAlbumResponseAlbum Album { get; set; }
}

public record GetAlbumResponseAlbum
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Name { get; set; }
    public required int? Year { get; set; }
    public required long ArtistId { get; set; }
    public required string ArtistName { get; set; }
    public required int SongsCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required List<GetAlbumResponseSong> Songs { get; set; }

    public static GetAlbumResponseAlbum FromEntity(Entities.Album album)
    {
        return new GetAlbumResponseAlbum
        {
            Id = album.Id,
            Cover = album.CoverId,
            Name = album.Name,
            Year = album.Year,
            ArtistId = album.ArtistId,
            ArtistName = album.Artist.Name,
            SongsCount = album.SongsCount,
            CreatedAt = album.CreatedAt,
            Songs = album.Songs.Select(GetAlbumResponseSong.FromEntity).ToList(),
        };
    }
}

public record GetAlbumResponseSong
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Title { get; set; }
    public required List<GetAlbumResponseArtist> Artists { get; set; }
    public required GetAlbumResponseSongAlbum Album { get; set; }
    public required List<GetAlbumResponseGenre> Genres { get; set; }
    public required int? Year { get; set; }
    public required string Duration { get; set; }
    public required bool IsFavorite { get; set; }
    public required bool IsExplicit { get; set; }

    public static GetAlbumResponseSong FromEntity(Entities.Song song)
    {
        return new GetAlbumResponseSong
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = song.Artists.Select(GetAlbumResponseArtist.FromEntity).ToList(),
            Album = GetAlbumResponseSongAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(GetAlbumResponseGenre.FromEntity).ToList(),
            Year = song.Year,
            Duration = song.Duration.ToString(@"mm\:ss"),
            IsFavorite = false,
            IsExplicit = song.Explicit,
        };
    }
}

public record GetAlbumResponseGenre
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetAlbumResponseGenre FromEntity(Entities.SongGenre songGenre)
    {
        return new GetAlbumResponseGenre
        {
            Id = songGenre.GenreId,
            Name = songGenre.Genre.Name,
        };
    }
}

public record GetAlbumResponseSongAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetAlbumResponseSongAlbum FromEntity(Entities.Album album)
    {
        return new GetAlbumResponseSongAlbum
        {
            Id = album.Id,
            Name = album.Name,
        };
    }
}

public record GetAlbumResponseArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetAlbumResponseArtist FromEntity(Entities.SongArtist songArtist)
    {
        return new GetAlbumResponseArtist
        {
            Id = songArtist.ArtistId,
            Name = songArtist.Artist.Name,
        };
    }
}