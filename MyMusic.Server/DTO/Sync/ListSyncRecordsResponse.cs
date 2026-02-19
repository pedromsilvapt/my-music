using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record ListSyncRecordsResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
}

public record SyncRecordResponseItem
{
    public required string FilePath { get; init; }
    public required SyncRecordAction Action { get; init; }
    public required SyncRecordSource Source { get; init; }
    public long? SongId { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Reason { get; init; }
    public DateTime ProcessedAt { get; init; }

    public static SyncRecordResponseItem FromEntity(DeviceSyncSessionRecord record) =>
        new()
        {
            FilePath = record.FilePath,
            Action = record.Action,
            Source = record.Source,
            SongId = record.SongId,
            ErrorMessage = record.ErrorMessage,
            Reason = record.Reason,
            ProcessedAt = record.ProcessedAt,
        };
}