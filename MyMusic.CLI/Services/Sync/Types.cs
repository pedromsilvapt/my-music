namespace MyMusic.CLI.Services.Sync.Types;

public enum SyncDirection
{
    Both = 0,
    Up = 1,
    Down = 2
}

public enum ConflictResolution
{
    Upload,
    Download,
    Skip
}

public record SyncOptions
{
    public bool Force { get; init; }
    public bool DryRun { get; init; }
    public bool AutoConfirm { get; init; }
    public bool Verbose { get; init; }
    public SyncDirection Direction { get; init; } = SyncDirection.Both;
}

public record SyncResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Downloaded { get; init; }
    public int Removed { get; init; }
    public int Failed { get; init; }
    public int Conflicts { get; init; }
    public bool Cancelled { get; init; }
    public long? SessionId { get; init; }

    public static SyncResult Empty => new();
}

public record SyncProgress
{
    public required SyncResult Result { get; init; }
    public string Phase { get; init; } = "";
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public string CurrentFile { get; init; } = "";
    public string? ErrorMessage { get; init; }

    public static SyncProgress ForPhase(string phase, string? currentFile = null) =>
        new()
        {
            Result = SyncResult.Empty,
            Phase = phase,
            CurrentFile = currentFile ?? ""
        };

    public static SyncProgress ForScanProgress(int processedFiles, string currentDir) =>
        new()
        {
            Result = SyncResult.Empty,
            Phase = "scanning",
            ProcessedFiles = processedFiles,
            CurrentFile = currentDir
        };

    public static SyncProgress FromResult(
        SyncResult result,
        string phase,
        int totalFiles,
        int processedFiles,
        string? currentFile = null,
        string? errorMessage = null) =>
        new()
        {
            Result = result,
            Phase = phase,
            TotalFiles = totalFiles,
            ProcessedFiles = processedFiles,
            CurrentFile = currentFile ?? "",
            ErrorMessage = errorMessage
        };
}

public record RecordItem
{
    public required string FilePath { get; init; }
    public required string Action { get; init; }
    public string Source { get; init; } = "";
    public string? Reason { get; init; }
    public string? ErrorMessage { get; init; }
    public long? SongId { get; init; }
}

public record PendingActionItem
{
    public long? SongId { get; init; }
    public required string Path { get; init; }
    public required string Action { get; init; }
    public string? PreviousPath { get; init; }
}

public record SyncFileInfo
{
    public required string Path { get; init; }
    public required DateTime ModifiedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Reason { get; init; }
}

public record SyncConflictItem
{
    public required string Path { get; init; }
    public string Reason { get; init; } = "";
}

public record StartSyncRequest
{
    public bool DryRun { get; init; }
    public string? RepositoryPath { get; init; }
}

public record StartSyncResult
{
    public long SessionId { get; init; }
}

public record CheckSyncRequest
{
    public required List<SyncFileInfo> Files { get; init; }
    public bool Force { get; init; }
}

public record CheckSyncResult
{
    public required List<SyncFileInfo> ToCreate { get; init; }
    public required List<SyncFileInfo> ToUpdate { get; init; }
    public required List<PotentialConflictItem> PotentialConflicts { get; init; }
    public required List<PendingActionItem> PendingActions { get; init; }
}

public record PotentialConflictItem
{
    public required string Path { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
    public required DateTime ServerModifiedAt { get; init; }
    public required DateTime? LastSyncedAt { get; init; }
    public required long SongId { get; init; }
    public required string ServerChecksum { get; init; }
    public required string ServerChecksumAlgorithm { get; init; }
}

public record UploadFileRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string Path { get; init; }
    public required string ModifiedAt { get; init; }
    public required string CreatedAt { get; init; }
}

public record UploadFileResult
{
    public bool Success { get; init; }
    public long? SongId { get; init; }
    public List<PendingActionItem> PendingActions { get; init; } = [];
}

public record RecordChunkRequest
{
    public required List<RecordItem> Records { get; init; }
}

public record CompleteSyncRequest
{
    public required string Direction { get; init; }
}

public record CompleteSyncResult
{
    public int CreatedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int DownloadedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ErrorCount { get; init; }
}

public record GetPendingActionsResult
{
    public required List<PendingActionItem> Actions { get; init; }
}

public record AcknowledgeActionRequest
{
    public required string DevicePath { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public string? PreviousDevicePath { get; init; }
}

public record ResolveConflictsRequest
{
    public required List<ConflictResolveItem> Conflicts { get; init; }
}

public record ConflictResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    public required string FileContentBase64 { get; init; }
    public required string LocalModifiedAt { get; init; }
}

public record ResolveConflictsResult
{
    public required List<SyncFileInfo> ToUpload { get; init; }
    public required List<ResolvedConflictItem> Resolved { get; init; }
    public required List<SyncConflictItem> Conflicts { get; init; }
}

public record ResolvedConflictItem
{
    public required string Path { get; init; }
    public required DateTime ModifiedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Reason { get; init; }
}

public record ScannedFile
{
    public required string RelativePath { get; init; }
    public required string FullPath { get; init; }
    public required DateTime ModifiedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public long Size { get; init; }
}

public record ScanError
{
    public required string Path { get; init; }
    public required string Error { get; init; }
}

public record ScanResult
{
    public required List<ScannedFile> Files { get; init; }
    public required List<ScanError> Errors { get; init; }
}