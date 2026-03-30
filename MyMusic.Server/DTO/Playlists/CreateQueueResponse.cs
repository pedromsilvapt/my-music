namespace MyMusic.Server.DTO.Playlists;

public record CreateQueueResponse
{
    public required GetPlaylistItem Queue { get; init; }
}