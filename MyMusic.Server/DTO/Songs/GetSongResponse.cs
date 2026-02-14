using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record GetSongResponse
{
    public required GetSongResponseSong Song { get; set; }
}

public record GetSongResponseSong
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Title { get; set; }
    public required string Label { get; set; }
    public required List<GetSongResponseArtist> Artists { get; set; }
    public required GetSongResponseAlbum Album { get; set; }
    public required List<GetSongResponseGenre> Genres { get; set; }
    public required int? Year { get; set; }
    public required string Duration { get; set; }
    public required bool IsFavorite { get; set; }
    public required bool IsExplicit { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? AddedAt { get; set; }
    public string? Lyrics { get; set; }

    public static GetSongResponseSong FromEntity(Entities.Song song)
    {
        return new GetSongResponseSong
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Label = song.Label,
            Artists = song.Artists.Select(GetSongResponseArtist.FromEntity).ToList(),
            Album = GetSongResponseAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(GetSongResponseGenre.FromEntity).ToList(),
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            IsFavorite = false,
            IsExplicit = song.Explicit,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt,
            Lyrics = song.Lyrics,
        };
    }
}

public record GetSongResponseArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetSongResponseArtist FromEntity(Entities.SongArtist songArtist)
    {
        return new GetSongResponseArtist
        {
            Id = songArtist.ArtistId,
            Name = songArtist.Artist.Name,
        };
    }
}

public record GetSongResponseAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required int? Year { get; set; }

    public static GetSongResponseAlbum FromEntity(Entities.Album album)
    {
        return new GetSongResponseAlbum
        {
            Id = album.Id,
            Name = album.Name,
            Year = album.Year,
        };
    }
}

public record GetSongResponseGenre
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static GetSongResponseGenre FromEntity(Entities.SongGenre songGenre)
    {
        return new GetSongResponseGenre
        {
            Id = songGenre.GenreId,
            Name = songGenre.Genre.Name,
        };
    }
}