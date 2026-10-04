using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Result of a sync conflict-choices operation. The controller maps this to
/// <c>SyncConflictChoicesResponse</c>.
/// </summary>
public record SyncConflictChooseResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
}

/// <summary>
/// Applies the user's choice to resolve real conflicts (<c>Conflict</c> records of a session in progress)
/// with the server's version. The choice to keep the local version needs no service of its own: the
/// client uploads the file, naming the conflict it resolves (see <see cref="ISyncUploadService"/>).
/// </summary>
public interface ISyncConflictChooseService
{
    /// <summary>
    /// Resolves the conflicts <paramref name="conflictRecordIds"/> of <paramref name="sessionId"/> by
    /// downloading the server's version: each one gets an <c>UpdateLocal</c> record (followed by a
    /// <c>Rename</c> record when the naming template changed the target path) that points to it through
    /// <see cref="DeviceSyncSessionRecord.ResolvesConflictRecordId"/>. In <c>up</c> direction the device
    /// is never changed, so a <c>Skipped</c> record is created instead and the conflict stays unresolved.
    /// Ids that are not a conflict of the session, or that are already resolved, are ignored.
    /// Returns <c>null</c> when no such device or session exists for <paramref name="ownerId"/>, and
    /// throws when the session is not in progress.
    /// </summary>
    Task<SyncConflictChooseResult?> ChooseDownloadAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        IReadOnlyCollection<long> conflictRecordIds,
        CancellationToken cancellationToken);
}
