using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record ListQueuesResponse
{
    public required List<ListQueueItem> Queues { get; init; }
}

public record ListQueueItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required int SongCount { get; init; }
    public long? CurrentSongId { get; init; }
    public required DateTime CreatedAt { get; init; }

    public static ListQueueItem FromEntity(Playlist playlist) =>
        new()
        {
            Id = playlist.Id,
            Name = playlist.Name,
            SongCount = playlist.PlaylistSongs.Count,
            CurrentSongId = playlist.CurrentSongId,
            CreatedAt = playlist.CreatedAt,
        };
}