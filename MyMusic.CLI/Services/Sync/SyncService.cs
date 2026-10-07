namespace MyMusic.CLI.Services.Sync;

using Microsoft.Extensions.DependencyInjection;
using MyMusic.CLI.Services.Sync.Types;

public class SyncService(IServiceProvider serviceProvider) : ISyncService
{
    public async Task<SyncResult> SyncAsync(bool force, bool dryRun, bool autoConfirm,
        SyncDirection direction, bool deduplicate, bool recordSkipped, ConflictResolution? conflicts = null,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var options = new SyncOptions
        {
            Force = force,
            DryRun = dryRun,
            AutoConfirm = autoConfirm,
            Direction = direction,
            Deduplicate = deduplicate,
            RecordSkipped = recordSkipped,
            Conflicts = conflicts,
        };

        var orchestrator = serviceProvider.GetRequiredService<Orchestrator>();
        return await orchestrator.OrchestrateSyncAsync(options, progress, ct);
    }
}
