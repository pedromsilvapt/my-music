using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record AcknowledgeActionRequest
{
    public required List<long> RecordIds { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

public record AcknowledgeActionResponse
{
    public required bool Success { get; init; }
    public required SyncActionCounts Counts { get; init; }
}

public record SyncActionRecordResponseItem
{
    public required long Id { get; init; }
    public required string Action { get; init; }
    public System.Text.Json.JsonElement? Data { get; init; }
    public long? ResolvesConflictRecordId { get; init; }

    public static SyncActionRecordResponseItem FromEntity(DeviceSyncSessionRecord record) => new()
    {
        Id = record.Id,
        Action = record.Action.ToString(),
        Data = record.Data,
        ResolvesConflictRecordId = record.ResolvesConflictRecordId,
    };
}