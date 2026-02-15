using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record ListPlaylistsResponse
{
    public required List<ListPlaylistItem> Playlists { get; set; }
}

public record ListPlaylistItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required int SongCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public static ListPlaylistItem FromEntity(Entities.Playlist playlist)
    {
        return new ListPlaylistItem
        {
            Id = playlist.Id,
            Name = playlist.Name,
            SongCount = playlist.PlaylistSongs.Count,
            CreatedAt = playlist.CreatedAt,
            ModifiedAt = playlist.ModifiedAt
        };
    }
}