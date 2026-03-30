namespace MyMusic.Server.DTO.Playlists;

public record CreateQueueRequest
{
    public required List<long> SongIds { get; init; }
    public long? CurrentSongId { get; init; }
    public string? Name { get; init; }
}