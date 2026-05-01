namespace MyMusic.CLI.Services;

using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;

public interface ISyncService
{
    Task<SyncResult> SyncAsync(bool force, bool verbose, bool dryRun, bool autoConfirm,
        SyncDirection direction, IProgress<SyncProgress>? progress = null, CancellationToken ct = default);
}
