namespace MyMusic.CLI.Api.Dtos;

public record ListSyncRecordsResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
}

public record SyncRecordResponseItem
{
    public required string FilePath { get; init; }
    public required string Action { get; init; }
    public required string Source { get; init; }
    public long? SongId { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Reason { get; init; }
    public DateTime ProcessedAt { get; init; }
}