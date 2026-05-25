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
    public required int CreateRemoteCount { get; init; }
    public required int UpdateRemoteCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int CreateLocalCount { get; init; }
    public required int UpdateLocalCount { get; init; }
    public required int DeleteCount { get; init; }
    public required int LinkCount { get; init; }
    public required int UnlinkCount { get; init; }
    public required int RenameCount { get; init; }
    public required int ConflictCount { get; init; }
    public required int UpdateTimestampCount { get; init; }
    public required int ErrorCount { get; init; }
    public string? RepositoryPath { get; init; }

    public static SyncSessionItem FromEntity(DeviceSyncSession session)
    {
        return new SyncSessionItem
        {
            Id = session.Id,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            Status = session.Status,
            IsDryRun = session.IsDryRun,
            CreateRemoteCount = session.Records.Count(r => r.Action == SyncRecordAction.CreateRemote),
            UpdateRemoteCount = session.Records.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            SkippedCount = session.Records.Count(r => r.Action == SyncRecordAction.Skipped),
            CreateLocalCount = session.Records.Count(r => r.Action == SyncRecordAction.CreateLocal),
            UpdateLocalCount = session.Records.Count(r => r.Action == SyncRecordAction.UpdateLocal),
            DeleteCount = session.Records.Count(r => r.Action == SyncRecordAction.Delete),
            LinkCount = session.Records.Count(r => r.Action == SyncRecordAction.Link),
            UnlinkCount = session.Records.Count(r => r.Action == SyncRecordAction.Unlink),
            RenameCount = session.Records.Count(r => r.Action == SyncRecordAction.Rename),
            ConflictCount = session.Records.Count(r => r.Action == SyncRecordAction.Conflict),
            UpdateTimestampCount = session.Records.Count(r => r.Action == SyncRecordAction.UpdateTimestamp),
            ErrorCount = session.Records.Count(r => r.Action == SyncRecordAction.Error),
            RepositoryPath = session.RepositoryPath,
        };
    }
}