namespace MyMusic.Server.DTO.SongSharing;

public record CreateSongShareRequest
{
    public required long UserId { get; init; }
}