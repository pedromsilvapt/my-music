namespace MyMusic.CLI.Services;

using MyMusic.CLI.Services.Sync.Types;

public interface ISyncService
{
    Task<SyncResult> SyncAsync(bool force, bool dryRun, bool autoConfirm,
        SyncDirection direction, bool deduplicate, bool recordSkipped, ConflictResolution? conflicts = null,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default);
}
