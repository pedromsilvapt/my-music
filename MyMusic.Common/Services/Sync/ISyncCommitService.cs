using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public interface ISyncCommitService
{
    /// <summary>
    /// Executes every record of the session. Orphan handling follows the
    /// <see cref="DeviceSyncSession.Direction"/> stored on the session when it was started.
    /// </summary>
    Task<SyncCommitResult> CommitAsync(MusicDbContext db, long sessionId, long deviceId, bool isDryRun, CancellationToken cancellationToken = default);

    Task AcknowledgeRecordsAsync(List<DeviceSyncSessionRecord> records, DateTime? modifiedAt);
}

public class SyncCommitResult
{
    public required Dictionary<SyncRecordAction, int> ActionCounts { get; set; }

    public required DateTime CommittedAt { get; set; }
}