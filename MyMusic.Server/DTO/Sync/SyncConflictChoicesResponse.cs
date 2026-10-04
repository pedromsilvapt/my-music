namespace MyMusic.Server.DTO.Sync;

public record SyncConflictChoicesResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
    public required SyncActionCounts Counts { get; init; }
}
