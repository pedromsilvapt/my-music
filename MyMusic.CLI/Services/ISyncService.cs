namespace MyMusic.CLI.Services;

public record SyncProgress(
    int TotalFiles,
    int ProcessedFiles,
    string CurrentFile,
    int Created,
    int Updated,
    int Skipped,
    int Downloaded,
    int Removed,
    int Failed,
    string Phase);

public record SyncResult(int Created, int Updated, int Skipped, int Downloaded, int Removed, int Failed);

public interface ISyncService
{
    Task<SyncResult> SyncAsync(bool force, bool verbose, bool dryRun, bool autoConfirm,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default);
}