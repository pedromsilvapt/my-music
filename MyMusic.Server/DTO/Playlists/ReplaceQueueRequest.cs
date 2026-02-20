namespace MyMusic.Server.DTO.Playlists;

public record ReplaceQueueRequest
{
    public required List<long> SongIds { get; init; }
    public long? CurrentSongId { get; init; }
}