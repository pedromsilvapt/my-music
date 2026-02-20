using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record ListPlaylistsResponse
{
    public required List<ListPlaylistItem> Playlists { get; set; }
}

public record ListPlaylistItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required PlaylistType Type { get; set; }
    public required int SongCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public static ListPlaylistItem FromEntity(Playlist playlist) =>
        new()
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Type = playlist.Type,
            SongCount = playlist.PlaylistSongs.Count,
            CreatedAt = playlist.CreatedAt,
            ModifiedAt = playlist.ModifiedAt,
        };
}