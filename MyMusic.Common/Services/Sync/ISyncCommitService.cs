using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public interface ISyncCommitService
{
    Task<SyncCommitResult> CommitAsync(MusicDbContext db, long sessionId, long deviceId, bool isDryRun, string direction = "both", CancellationToken cancellationToken = default);

    Task AcknowledgeRecordsAsync(List<DeviceSyncSessionRecord> records, DateTime? modifiedAt);
}

public class SyncCommitResult
{
    public required Dictionary<SyncRecordAction, int> ActionCounts { get; set; }

    public required DateTime CommittedAt { get; set; }
}