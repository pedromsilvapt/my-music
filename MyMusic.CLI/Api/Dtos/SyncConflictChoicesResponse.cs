namespace MyMusic.CLI.Api.Dtos;

public record SyncConflictChoicesResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
    public required SyncActionCounts Counts { get; init; }
}
