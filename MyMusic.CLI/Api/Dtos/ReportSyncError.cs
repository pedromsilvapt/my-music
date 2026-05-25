namespace MyMusic.CLI.Api.Dtos;

public record ReportSyncErrorRequest
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
    public long? SongId { get; init; }
}

public record ReportSyncErrorResponse
{
    public required SyncActionCounts Counts { get; init; }
}