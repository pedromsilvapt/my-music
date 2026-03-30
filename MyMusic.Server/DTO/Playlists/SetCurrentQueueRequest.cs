namespace MyMusic.Server.DTO.Playlists;

public record SetCurrentQueueRequest
{
    public required long QueueId { get; init; }
}