using MyMusic.Common.Entities;
using MyMusic.Server.DTO.Songs;
using SongEntity = MyMusic.Common.Entities.Song;

namespace MyMusic.Server.DTO.Playlists;

public record GetPlaylistResponse
{
    public required GetPlaylistItem Playlist { get; init; }
}

public record GetPlaylistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required PlaylistType Type { get; init; }
    public long? CurrentSongId { get; init; }
    public required List<GetPlaylistSong> Songs { get; init; }

    public static GetPlaylistItem FromEntity(Playlist playlist) =>
        new()
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Type = playlist.Type,
            CurrentSongId = playlist.CurrentSongId,
            Songs = playlist.PlaylistSongs
                .OrderBy(ps => ps.Order)
                .Select(ps => GetPlaylistSong.FromEntity(ps.Song, ps.Order, ps.AddedAt))
                .ToList(),
        };
}

public record GetPlaylistSong : ListSongsItem
{
    public required int Order { get; init; }
    public DateTime? AddedAtPlaylist { get; init; }

    public static GetPlaylistSong FromEntity(SongEntity song, int order, DateTime addedAt) =>
        new()
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList(),
            Album = ListSongsAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(g => ListSongsGenre.FromEntity(g.Genre)).ToList(),
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            IsFavorite = false,
            IsExplicit = song.Explicit,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt,
            Order = order,
            AddedAtPlaylist = addedAt,
        };
}