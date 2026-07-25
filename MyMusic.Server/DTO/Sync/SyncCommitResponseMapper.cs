using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Server.DTO.Sync;

/// <summary>
/// Maps a <see cref="SyncCommitResult"/> (or the raw session records of an already-committed
/// session) into a <see cref="SyncCommitResponse"/>. Extracted from DevicesController.CommitSync
/// as part of Phase 9 of the controllers refactor so the controller stays thin
/// (input/output + DTO mapping only).
/// </summary>
public static class SyncCommitResponseMapper
{
    /// <summary>
    /// Maps the per-action counts of a fresh commit (<see cref="SyncCommitResult"/>) into a
    /// <see cref="SyncCommitResponse"/>, using <paramref name="committedAt"/> as the
    /// <see cref="SyncCommitResponse.CommittedAt"/>.
    /// </summary>
    public static SyncCommitResponse Map(SyncCommitResult result, DateTime committedAt)
        => Map(result.ActionCounts, committedAt);

    /// <summary>
    /// Maps the records of an already-committed session into a <see cref="SyncCommitResponse"/>,
    /// grouping records by <see cref="DeviceSyncSessionRecord.Action"/> and using
    /// <paramref name="committedAt"/> as the <see cref="SyncCommitResponse.CommittedAt"/>.
    /// </summary>
    public static SyncCommitResponse Map(List<DeviceSyncSessionRecord> records, DateTime committedAt)
        => Map(records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count()), committedAt);

    private static SyncCommitResponse Map(Dictionary<SyncRecordAction, int> counts, DateTime committedAt)
    {
        return new SyncCommitResponse
        {
            CreateRemoteCount = counts.GetValueOrDefault(SyncRecordAction.CreateRemote),
            UpdateRemoteCount = counts.GetValueOrDefault(SyncRecordAction.UpdateRemote),
            SkippedCount = counts.GetValueOrDefault(SyncRecordAction.Skipped),
            CreateLocalCount = counts.GetValueOrDefault(SyncRecordAction.CreateLocal),
            UpdateLocalCount = counts.GetValueOrDefault(SyncRecordAction.UpdateLocal),
            DeleteLocalCount = counts.GetValueOrDefault(SyncRecordAction.DeleteLocal),
            LinkCount = counts.GetValueOrDefault(SyncRecordAction.Link),
            UnlinkCount = counts.GetValueOrDefault(SyncRecordAction.Unlink),
            RenameCount = counts.GetValueOrDefault(SyncRecordAction.Rename),
            ConflictCount = counts.GetValueOrDefault(SyncRecordAction.Conflict),
            UpdateTimestampCount = counts.GetValueOrDefault(SyncRecordAction.UpdateTimestamp),
            ErrorCount = counts.GetValueOrDefault(SyncRecordAction.Error),
            CommittedAt = committedAt,
        };
    }
}