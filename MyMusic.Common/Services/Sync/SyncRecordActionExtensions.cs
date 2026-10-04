using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public static class SyncRecordActionExtensions
{
    /// <summary>
    /// Whether records of this action are performed by the client on the device, and therefore
    /// must be acknowledged by the client before the session can be committed.
    /// </summary>
    public static bool IsClientAction(this SyncRecordAction action) =>
        action is SyncRecordAction.CreateLocal or SyncRecordAction.UpdateLocal
            or SyncRecordAction.Unlink or SyncRecordAction.Rename or SyncRecordAction.DeleteLocal;

    /// <summary>
    /// Counts the conflicts left unresolved: the <c>Conflict</c> records minus the conflicts that other
    /// records resolve (<see cref="DeviceSyncSessionRecord.ResolvesConflictRecordId"/>). Over the records
    /// one request created, the result is a delta, negative when they resolve earlier conflicts.
    /// </summary>
    public static int CountUnresolvedConflicts(this IEnumerable<DeviceSyncSessionRecord> records)
    {
        var conflicts = 0;
        var resolved = new HashSet<long>();

        foreach (var record in records)
        {
            if (record.Action == SyncRecordAction.Conflict) conflicts++;
            if (record.ResolvesConflictRecordId.HasValue) resolved.Add(record.ResolvesConflictRecordId.Value);
        }

        return conflicts - resolved.Count;
    }

    /// <summary>
    /// Counts the records of each action. <c>Conflict</c> holds only the unresolved conflicts
    /// (see <see cref="CountUnresolvedConflicts"/>).
    /// </summary>
    public static Dictionary<SyncRecordAction, int> CountByAction(this IReadOnlyCollection<DeviceSyncSessionRecord> records)
    {
        var counts = records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count());
        var unresolvedConflicts = records.CountUnresolvedConflicts();

        if (unresolvedConflicts != 0 || counts.ContainsKey(SyncRecordAction.Conflict))
        {
            counts[SyncRecordAction.Conflict] = unresolvedConflicts;
        }

        return counts;
    }
}
