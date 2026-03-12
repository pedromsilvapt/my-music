namespace MyMusic.Server.DTO.Songs;

public record UploadSongResponse
{
    public required bool Success { get; init; }
    public long? SongId { get; init; }
    public string? Error { get; init; }
}