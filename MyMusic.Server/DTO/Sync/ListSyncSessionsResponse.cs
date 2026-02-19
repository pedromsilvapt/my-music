using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record ListSyncSessionsResponse
{
    public required List<SyncSessionItem> Sessions { get; init; }
}

public record SyncSessionItem
{
    public required long Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required SyncSessionStatus Status { get; init; }
    public required bool IsDryRun { get; init; }
    public required int CreatedCount { get; init; }
    public required int UpdatedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int DownloadedCount { get; init; }
    public required int RemovedCount { get; init; }
    public required int ErrorCount { get; init; }

    public static SyncSessionItem FromEntity(DeviceSyncSession session)
    {
        return new SyncSessionItem
        {
            Id = session.Id,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            Status = session.Status,
            IsDryRun = session.IsDryRun,
            CreatedCount = session.Records.Count(r => r.Action == SyncRecordAction.Created),
            UpdatedCount = session.Records.Count(r => r.Action == SyncRecordAction.Updated),
            SkippedCount = session.Records.Count(r => r.Action == SyncRecordAction.Skipped),
            DownloadedCount = session.Records.Count(r => r.Action == SyncRecordAction.Downloaded),
            RemovedCount = session.Records.Count(r => r.Action == SyncRecordAction.Removed),
            ErrorCount = session.Records.Count(r => r.Action == SyncRecordAction.Error),
        };
    }
}