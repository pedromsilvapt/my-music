namespace MyMusic.Server.DTO.Playlists;

public record ShuffleQueueRequest
{
    public required List<int> Indices { get; init; }
}