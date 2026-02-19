namespace MyMusic.CLI.Api.Dtos;

public record SyncRecordRequestItem
{
    public required string FilePath { get; init; }
    public required string Action { get; init; }
    public long? SongId { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Source { get; init; }
    public string? Reason { get; init; }
}

public record SyncRecordsRequest
{
    public required List<SyncRecordRequestItem> Records { get; init; }
}