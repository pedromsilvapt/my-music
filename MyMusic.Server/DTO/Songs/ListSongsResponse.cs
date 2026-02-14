using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record ListSongsResponse
{
    public required IEnumerable<ListSongsItem> Songs { get; set; }
}

public record ListSongsItem
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Title { get; set; }
    public required IEnumerable<ListSongsArtist> Artists { get; set; }
    public required ListSongsAlbum Album { get; set; }
    public required IEnumerable<ListSongsGenre> Genres { get; set; }
    public required int? Year { get; set; }
    public required string Duration { get; set; }
    public required bool IsFavorite { get; set; }
    public required bool IsExplicit { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? AddedAt { get; set; }

    public static ListSongsItem FromEntity(Entities.Song song)
    {
        var artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList();
        var genres = song.Genres.Select(g => ListSongsGenre.FromEntity(g.Genre)).ToList();
        var album = ListSongsAlbum.FromEntity(song.Album);

        return new ListSongsItem
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = artists,
            Album = album,
            Genres = genres,
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            IsFavorite = false,
            IsExplicit = song.Explicit,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt
        };
    }
}

public record ListSongsArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static ListSongsArtist FromEntity(Entities.Artist artist)
    {
        return new ListSongsArtist
        {
            Id = artist.Id,
            Name = artist.Name,
        };
    }
}

public record ListSongsAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static ListSongsAlbum FromEntity(Entities.Album album)
    {
        return new ListSongsAlbum
        {
            Id = album.Id,
            Name = album.Name,
        };
    }
}

public record ListSongsGenre
{
    public required long Id { get; set; }
    public required string Name { get; set; }

    public static ListSongsGenre FromEntity(Entities.Genre genre)
    {
        return new ListSongsGenre
        {
            Id = genre.Id,
            Name = genre.Name,
        };
    }
}