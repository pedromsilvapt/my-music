namespace MyMusic.CLI.Services.Sync.Types;

public enum SyncSessionStatus
{
    InProgress,
    Committed,
    Completed,
    Cancelled,
}

public enum SyncRecordAction
{
    CreateRemote,
    UpdateRemote,
    CreateLocal,
    UpdateLocal,
    DeleteLocal,
    Link,
    Unlink,
    Rename,
    Skipped,
    Conflict,
    UpdateTimestamp,
    Error,
}

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
    public SyncDirection Direction { get; init; } = SyncDirection.Both;
    public bool Deduplicate { get; init; }
    /// <summary>
    /// Keep the session's Skipped records after it completes. By default the server deletes them.
    /// </summary>
    public bool RecordSkipped { get; init; }
    /// <summary>
    /// How real conflicts are resolved without asking. When null, the user is asked for each one.
    /// </summary>
    public ConflictResolution? Conflicts { get; init; }
}

public record SyncResult
{
    public int CreateRemote { get; init; }
    public int UpdateRemote { get; init; }
    public int CreateLocal { get; init; }
    public int UpdateLocal { get; init; }
    public int DeleteLocal { get; init; }
    public int Link { get; init; }
    public int Unlink { get; init; }
    public int Rename { get; init; }
    public int Skipped { get; init; }
    public int Conflict { get; init; }
    public int UpdateTimestamp { get; init; }
    public int Error { get; init; }
    public bool Cancelled { get; init; }
    public long? SessionId { get; init; }

    public static SyncResult Empty => new();

    public SyncResult AddDelta(SyncActionCounts delta) => new()
    {
        CreateRemote = CreateRemote + delta.CreateRemoteCount,
        UpdateRemote = UpdateRemote + delta.UpdateRemoteCount,
        CreateLocal = CreateLocal + delta.CreateLocalCount,
        UpdateLocal = UpdateLocal + delta.UpdateLocalCount,
        DeleteLocal = DeleteLocal + delta.DeleteLocalCount,
        Link = Link + delta.LinkCount,
        Unlink = Unlink + delta.UnlinkCount,
        Rename = Rename + delta.RenameCount,
        Skipped = Skipped + delta.SkippedCount,
        Conflict = Conflict + delta.ConflictCount,
        UpdateTimestamp = UpdateTimestamp + delta.UpdateTimestampCount,
        Error = Error + delta.ErrorCount,
    };
}

public record SyncActionCounts
{
    public int CreateRemoteCount { get; init; }
    public int UpdateRemoteCount { get; init; }
    public int SkippedCount { get; init; }
    public int CreateLocalCount { get; init; }
    public int UpdateLocalCount { get; init; }
    public int DeleteLocalCount { get; init; }
    public int LinkCount { get; init; }
    public int UnlinkCount { get; init; }
    public int RenameCount { get; init; }
    public int ConflictCount { get; init; }
    public int UpdateTimestampCount { get; init; }
    public int ErrorCount { get; init; }

    public static SyncActionCounts Empty => new();

    public SyncActionCounts Add(SyncActionCounts other) => new()
    {
        CreateRemoteCount = CreateRemoteCount + other.CreateRemoteCount,
        UpdateRemoteCount = UpdateRemoteCount + other.UpdateRemoteCount,
        SkippedCount = SkippedCount + other.SkippedCount,
        CreateLocalCount = CreateLocalCount + other.CreateLocalCount,
        UpdateLocalCount = UpdateLocalCount + other.UpdateLocalCount,
        DeleteLocalCount = DeleteLocalCount + other.DeleteLocalCount,
        LinkCount = LinkCount + other.LinkCount,
        UnlinkCount = UnlinkCount + other.UnlinkCount,
        RenameCount = RenameCount + other.RenameCount,
        ConflictCount = ConflictCount + other.ConflictCount,
        UpdateTimestampCount = UpdateTimestampCount + other.UpdateTimestampCount,
        ErrorCount = ErrorCount + other.ErrorCount,
    };

    public static SyncActionCounts FromApi(MyMusic.CLI.Api.Dtos.SyncActionCounts api) => new()
    {
        CreateRemoteCount = api.CreateRemoteCount,
        UpdateRemoteCount = api.UpdateRemoteCount,
        SkippedCount = api.SkippedCount,
        CreateLocalCount = api.CreateLocalCount,
        UpdateLocalCount = api.UpdateLocalCount,
        DeleteLocalCount = api.DeleteLocalCount,
        LinkCount = api.LinkCount,
        UnlinkCount = api.UnlinkCount,
        RenameCount = api.RenameCount,
        ConflictCount = api.ConflictCount,
        UpdateTimestampCount = api.UpdateTimestampCount,
        ErrorCount = api.ErrorCount,
    };
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

public record SyncRecordItem
{
    public required long Id { get; init; }
    public required string FilePath { get; init; }
    public required SyncRecordAction Action { get; init; }
    public long? SongId { get; init; }
    public System.Text.Json.JsonElement? Data { get; init; }
    public long? ResolvesConflictRecordId { get; init; }
    public SyncRecordSongInfo? SongInfo { get; init; }
    public string? Reason { get; init; }
    public bool Acknowledged { get; init; }
    public DateTime ProcessedAt { get; init; }

    public string Source => Action switch
    {
        SyncRecordAction.CreateLocal or SyncRecordAction.UpdateLocal or SyncRecordAction.Unlink or SyncRecordAction.Rename or SyncRecordAction.DeleteLocal => "Client",
        SyncRecordAction.CreateRemote or SyncRecordAction.UpdateRemote or SyncRecordAction.Link or SyncRecordAction.Skipped or SyncRecordAction.Conflict or SyncRecordAction.UpdateTimestamp or SyncRecordAction.Error => "Server",
        _ => throw new InvalidOperationException($"Unknown sync record action: {Action}")
    };
}

public record SyncRecordSongInfo
{
    public required long Id { get; init; }
    public required string Title { get; init; }
    public required string ArtistNames { get; init; }
    public string? CoverId { get; init; }
}

public record SyncFileInfo
{
    public required string Path { get; init; }
    public required DateTime ModifiedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Reason { get; init; }
}


public record StartSyncRequest
{
    public bool DryRun { get; init; }
    public SyncDirection Direction { get; init; } = SyncDirection.Both;
    public string? RepositoryPath { get; init; }
    public bool Deduplicate { get; init; }
    public bool RecordSkipped { get; init; }
    public List<ScanError>? ScanErrors { get; init; }

    /// <summary>
    /// Device options to preview in a dry run. They aren't saved to the device, so the server only
    /// accepts them when <see cref="DryRun"/> is set.
    /// </summary>
    public StartSyncDeviceOptions? DeviceOptions { get; init; }
}

public record StartSyncDeviceOptions
{
    public string? NamingTemplate { get; init; }
}

public record StartSyncResult
{
    public long SessionId { get; init; }
}

public record PrepareDeduplicateResult
{
    public int Total { get; init; }
    public int Processed { get; init; }
    public bool Done { get; init; }
}

public record CheckSyncRequest
{
    public required List<SyncFileInfo> Files { get; init; }
    public bool Force { get; init; }
}

public record CheckSyncResult
{
    public required List<SyncRecordItem> Records { get; init; }
    public required SyncActionCounts Counts { get; init; }
}

public record UploadFileRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string Path { get; init; }
    public required string ModifiedAt { get; init; }
    public required string CreatedAt { get; init; }
    /// <summary>The Conflict record the user resolved by keeping this local file.</summary>
    public long? ResolvesConflictRecordId { get; init; }
}

public record UploadFileResult
{
    public bool Success { get; init; }
    public long? SongId { get; init; }

    /// <summary>Every record the server created for the uploaded file, in order.</summary>
    public List<SyncRecordItem> Records { get; init; } = [];

    public SyncActionCounts Counts { get; init; } = SyncActionCounts.Empty;
}

public record CompleteSyncResult
{
    public int CreateRemoteCount { get; init; }
    public int UpdateRemoteCount { get; init; }
    public int SkippedCount { get; init; }
    public int CreateLocalCount { get; init; }
    public int UpdateLocalCount { get; init; }
    public int DeleteLocalCount { get; init; }
    public int LinkCount { get; init; }
    public int UnlinkCount { get; init; }
    public int RenameCount { get; init; }
    public int ConflictCount { get; init; }
    public int UpdateTimestampCount { get; init; }
    public int ErrorCount { get; init; }
}

public record CommitSyncResult
{
    public int CreateRemoteCount { get; init; }
    public int UpdateRemoteCount { get; init; }
    public int SkippedCount { get; init; }
    public int CreateLocalCount { get; init; }
    public int UpdateLocalCount { get; init; }
    public int DeleteLocalCount { get; init; }
    public int LinkCount { get; init; }
    public int UnlinkCount { get; init; }
    public int RenameCount { get; init; }
    public int ConflictCount { get; init; }
    public int UpdateTimestampCount { get; init; }
    public int ErrorCount { get; init; }
    public DateTime CommittedAt { get; init; }
}

public record CreatePendingActionsResult
{
    public required List<SyncRecordItem> Records { get; init; }
    public SyncActionCounts Counts { get; init; } = SyncActionCounts.Empty;
}

public record AcknowledgeActionRequest
{
    public required List<long> RecordIds { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

public record ResolveConflictsRequest
{
    public required List<ConflictResolveItem> Conflicts { get; init; }
    public required List<PotentialUpdateResolveItem> PotentialUpdates { get; init; }
}

public record ConflictResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    public required string Checksum { get; init; }
    public required string ChecksumAlgorithm { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
}

public record PotentialUpdateResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    public required string Checksum { get; init; }
    public required string ChecksumAlgorithm { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
    public required DateTime LastSyncedAt { get; init; }
}

public record AcknowledgeActionResult
{
    public required bool Success { get; init; }
    public SyncActionCounts Counts { get; init; } = SyncActionCounts.Empty;
}

public record ResolveConflictsResult
{
    public required List<SyncRecordItem> Records { get; init; }
    public SyncActionCounts Counts { get; init; } = SyncActionCounts.Empty;
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

public record ReportSyncErrorCliRequest
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
    public long? SongId { get; init; }

    /// <summary>
    /// The client-action record that failed. The server acknowledges it and does not apply it at commit.
    /// </summary>
    public long? RecordId { get; init; }
}

public record ScanResult
{
    public required List<ScannedFile> Files { get; init; }
    public required List<ScanError> Errors { get; init; }
}