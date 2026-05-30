namespace MyMusic.CLI.Api.Dtos;

public record SyncResolveConflictsResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
    public required SyncActionCounts Counts { get; init; }
}