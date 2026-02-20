namespace MyMusic.Server.DTO.Playlists;

public record RemoveFromQueueRequest
{
    public required List<long> SongIds { get; init; }
}