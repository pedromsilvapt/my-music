namespace MyMusic.CLI.Api.Dtos;

public record SyncUploadResponse
{
    public required bool Success { get; init; }
    public long? SongId { get; init; }
}