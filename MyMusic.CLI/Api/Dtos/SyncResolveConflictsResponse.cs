using System.Text.Json;

namespace MyMusic.CLI.Api.Dtos;

public record SyncConflictErrorItem
{
    public required string Path { get; init; }
    public required string Reason { get; init; }
}

public record SyncActionRecordResponseItem
{
    public required long Id { get; init; }
    public required string Action { get; init; }
    public JsonElement? Data { get; init; }
    public long? ResolvesConflictRecordId { get; init; }
}

public record SyncResolveConflictsResponse
{
    public required List<SyncFileInfoItem> ToUpload { get; init; }
    public required List<SyncFileInfoItem> Resolved { get; init; }
    public required List<SyncConflictErrorItem> Conflicts { get; init; }
    public required List<SyncActionRecordResponseItem> ConflictRecords { get; init; }
    public required List<SyncActionRecordResponseItem> UpdateTimestampRecords { get; init; }
    public required SyncActionCounts Counts { get; init; }
}