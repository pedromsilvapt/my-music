namespace MyMusic.Server.DTO.Playlists;

public record RenameQueueRequest
{
    public required string Name { get; init; }
}