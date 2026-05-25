namespace MyMusic.Server.DTO.Sync;

public record ReportSyncErrorRequest
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
    public long? SongId { get; init; }
}