using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record ListSongsResponse
{
    public required IEnumerable<ListSongItem> Songs { get; set; }
}

public record ListSongItem
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Title { get; set; }
    public required IEnumerable<ListSongsArtist> Artists { get; set; }
    public required ListSongsAlbum Album { get; set; }
    public required IEnumerable<ListSongsGenre> Genres { get; set; }
    public required int? Year { get; set; }
    public required string Duration { get; set; }
    public required IEnumerable<ListSongsDevice> Devices { get; set; }
    public required bool IsFavorite { get; set; }
    public required bool IsExplicit { get; set; }
    public required bool HasLyrics { get; set; }
    public required bool IsShared { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? AddedAt { get; set; }

    /// <summary>
    /// Maps a <see cref="Entities.Song"/> to a <see cref="ListSongItem"/>, computing
    /// <see cref="IsShared"/> against <paramref name="currentUserId"/> (true when the song is
    /// owned by another user, i.e. surfaced via sharing). Use this overload from song-list
    /// endpoints that may include shared songs.
    /// </summary>
    public static ListSongItem FromEntity(Entities.Song song, long currentUserId)
    {
        var artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList();
        var genres = song.Genres.Select(g => ListSongsGenre.FromEntity(g.Genre)).ToList();
        var album = ListSongsAlbum.FromEntity(song.Album);
        var devices = song.Devices.Select(d => ListSongsDevice.FromEntity(d.Device)).DistinctBy(d => d.Id).ToList();
        return new ListSongItem
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = artists,
            Album = album,
            Genres = genres,
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            Devices = devices,
            IsFavorite = song.IsFavorite,
            IsExplicit = song.Explicit,
            HasLyrics = song.HasLyrics,
            IsShared = song.OwnerId != currentUserId,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt
        };
    }

    /// <summary>
    /// Maps a <see cref="Entities.Song"/> to a <see cref="ListSongItem"/> assuming the caller is
    /// the owner (IsShared = false). Use <see cref="FromEntity(Entities.Song, long)"/> from
    /// endpoints that may surface shared songs. Kept for callers that operate only on the
    /// current user's own library (e.g. audit non-conformities, excluded pairs).
    /// </summary>
    public static ListSongItem FromEntity(Entities.Song song) => FromEntity(song, song.OwnerId);
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

public record ListSongsDevice
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }

    public static ListSongsDevice FromEntity(Entities.Device device)
    {
        return new ListSongsDevice
        {
            Id = device.Id,
            Name = device.Name,
            Icon = device.Icon,
            Color = device.Color
        };
    }
}