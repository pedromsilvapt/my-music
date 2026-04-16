namespace MyMusic.CLI.Services;

using MyMusic.Common.Services.Sync;
using MyMusic.Common.Services.Sync.Types;

public interface ISyncService
{
    Task<SyncResult> SyncAsync(bool force, bool verbose, bool dryRun, bool autoConfirm,
        SyncDirection direction, IProgress<SyncProgress>? progress = null, CancellationToken ct = default);
}
