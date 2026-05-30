using MyMusic.CLI.Services.Sync.Types;

namespace MyMusic.CLI.Api.Dtos;

public record SyncCheckResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
    public required SyncActionCounts Counts { get; init; }
}