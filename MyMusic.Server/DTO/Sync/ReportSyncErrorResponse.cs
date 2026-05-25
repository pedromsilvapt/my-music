namespace MyMusic.Server.DTO.Sync;

public record ReportSyncErrorResponse
{
    public required SyncActionCounts Counts { get; init; }
}