using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record SyncActionCounts
{
    public int CreateRemoteCount { get; init; }
    public int UpdateRemoteCount { get; init; }
    public int SkippedCount { get; init; }
    public int CreateLocalCount { get; init; }
    public int UpdateLocalCount { get; init; }
    public int DeleteCount { get; init; }
    public int LinkCount { get; init; }
    public int UnlinkCount { get; init; }
    public int RenameCount { get; init; }
    public int ConflictCount { get; init; }
    public int UpdateTimestampCount { get; init; }
    public int ErrorCount { get; init; }

    public static SyncActionCounts FromRecords(IEnumerable<DeviceSyncSessionRecord> records)
    {
        var counts = records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count());
        return new SyncActionCounts
        {
            CreateRemoteCount = counts.GetValueOrDefault(SyncRecordAction.CreateRemote),
            UpdateRemoteCount = counts.GetValueOrDefault(SyncRecordAction.UpdateRemote),
            SkippedCount = counts.GetValueOrDefault(SyncRecordAction.Skipped),
            CreateLocalCount = counts.GetValueOrDefault(SyncRecordAction.CreateLocal),
            UpdateLocalCount = counts.GetValueOrDefault(SyncRecordAction.UpdateLocal),
            DeleteCount = counts.GetValueOrDefault(SyncRecordAction.Delete),
            LinkCount = counts.GetValueOrDefault(SyncRecordAction.Link),
            UnlinkCount = counts.GetValueOrDefault(SyncRecordAction.Unlink),
            RenameCount = counts.GetValueOrDefault(SyncRecordAction.Rename),
            ConflictCount = counts.GetValueOrDefault(SyncRecordAction.Conflict),
            UpdateTimestampCount = counts.GetValueOrDefault(SyncRecordAction.UpdateTimestamp),
            ErrorCount = counts.GetValueOrDefault(SyncRecordAction.Error),
        };
    }

    public static SyncActionCounts FromAction(SyncRecordAction action, int count = 1)
    {
        return new SyncActionCounts
        {
            CreateRemoteCount = action == SyncRecordAction.CreateRemote ? count : 0,
            UpdateRemoteCount = action == SyncRecordAction.UpdateRemote ? count : 0,
            SkippedCount = action == SyncRecordAction.Skipped ? count : 0,
            CreateLocalCount = action == SyncRecordAction.CreateLocal ? count : 0,
            UpdateLocalCount = action == SyncRecordAction.UpdateLocal ? count : 0,
            DeleteCount = action == SyncRecordAction.Delete ? count : 0,
            LinkCount = action == SyncRecordAction.Link ? count : 0,
            UnlinkCount = action == SyncRecordAction.Unlink ? count : 0,
            RenameCount = action == SyncRecordAction.Rename ? count : 0,
            ConflictCount = action == SyncRecordAction.Conflict ? count : 0,
            UpdateTimestampCount = action == SyncRecordAction.UpdateTimestamp ? count : 0,
            ErrorCount = action == SyncRecordAction.Error ? count : 0,
        };
    }

    public SyncActionCounts Add(SyncActionCounts other)
    {
        return new SyncActionCounts
        {
            CreateRemoteCount = CreateRemoteCount + other.CreateRemoteCount,
            UpdateRemoteCount = UpdateRemoteCount + other.UpdateRemoteCount,
            SkippedCount = SkippedCount + other.SkippedCount,
            CreateLocalCount = CreateLocalCount + other.CreateLocalCount,
            UpdateLocalCount = UpdateLocalCount + other.UpdateLocalCount,
            DeleteCount = DeleteCount + other.DeleteCount,
            LinkCount = LinkCount + other.LinkCount,
            UnlinkCount = UnlinkCount + other.UnlinkCount,
            RenameCount = RenameCount + other.RenameCount,
            ConflictCount = ConflictCount + other.ConflictCount,
            UpdateTimestampCount = UpdateTimestampCount + other.UpdateTimestampCount,
            ErrorCount = ErrorCount + other.ErrorCount,
        };
    }
}