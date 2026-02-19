namespace MyMusic.Server.DTO.Sync;

public record SyncCompleteResponse
{
    public required int CreatedCount { get; init; }
    public required int UpdatedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int DownloadedCount { get; init; }
    public required int RemovedCount { get; init; }
    public required int ErrorCount { get; init; }
}