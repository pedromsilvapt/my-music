namespace MyMusic.CLI.Api.Dtos;

public record ListSyncSessionsResponse
{
    public required List<SyncSessionItem> Sessions { get; init; }
}

public record SyncSessionItem
{
    public required long Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required string Status { get; init; }
    public required bool IsDryRun { get; init; }
    public required int CreatedCount { get; init; }
    public required int UpdatedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int DownloadedCount { get; init; }
    public required int RemovedCount { get; init; }
    public required int ErrorCount { get; init; }
}