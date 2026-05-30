using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record SyncCheckResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
    public required SyncActionCounts Counts { get; init; }
}