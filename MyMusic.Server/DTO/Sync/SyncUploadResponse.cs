namespace MyMusic.Server.DTO.Sync;

public record SyncUploadResponse
{
    public required bool Success { get; init; }
    public long? SongId { get; init; }
}