namespace MyMusic.Server.DTO.Songs;

public record UploadSongRequest
{
    public required string Path { get; init; }
    public required string ModifiedAt { get; init; }
    public required string CreatedAt { get; init; }
}