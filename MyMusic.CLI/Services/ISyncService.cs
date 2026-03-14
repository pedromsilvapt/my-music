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
    string Phase,
    int Conflicts = 0);

public record SyncResult(int Created, int Updated, int Skipped, int Downloaded, int Removed, int Failed, int Conflicts = 0);

/// <summary>
/// Sync direction determines how repositories are synchronized.
/// </summary>
public enum SyncDirection
{
    /// <summary>
    /// Both directions: Merge changes from client and server.
    /// - Upload local changes to server
    /// - Process server actions (download/remove)
    /// - Remove orphaned songs from server (not on client, Action=null)
    /// </summary>
    Both,
    
    /// <summary>
    /// Up direction: Server mirrors client repository.
    /// - Upload local changes to server
    /// - Do NOT download anything
    /// - Remove ALL songs not on client from server (regardless of Action)
    /// - Clear all pending Actions for songs present on client
    /// </summary>
    Up,
    
    /// <summary>
    /// Down direction: Client mirrors server repository.
    /// - Do NOT upload anything to server
    /// - Download all songs from server (including Action=null)
    /// - Do NOT modify server (no orphaned removal, no action clearing)
    /// </summary>
    Down,
}

public interface ISyncService
{
    Task<SyncResult> SyncAsync(bool force, bool verbose, bool dryRun, bool autoConfirm,
        SyncDirection direction, IProgress<SyncProgress>? progress = null, CancellationToken ct = default);
}