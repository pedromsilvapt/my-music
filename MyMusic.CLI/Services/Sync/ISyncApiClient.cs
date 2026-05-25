namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface ISyncApiClient
{
    Task<StartSyncResult> StartSyncAsync(long deviceId, StartSyncRequest request, CancellationToken ct = default);
    Task<CheckSyncResult> CheckSyncAsync(long deviceId, long sessionId, CheckSyncRequest request, CancellationToken ct = default);
    Task<UploadFileResult> UploadFileAsync(long deviceId, long sessionId, UploadFileRequest request, CancellationToken ct = default);
    Task<CompleteSyncResult> CompleteSyncAsync(long deviceId, long sessionId, CompleteSyncRequest request, CancellationToken ct = default);
    Task<CommitSyncResult> CommitSyncAsync(long deviceId, long sessionId, CommitSyncRequest request, CancellationToken ct = default);
    Task<CreatePendingActionsResult> CreatePendingActionsAsync(long deviceId, long sessionId, CancellationToken ct = default);
    Task<AcknowledgeActionResult> AcknowledgeActionAsync(long deviceId, long sessionId, AcknowledgeActionRequest request, CancellationToken ct = default);
    Task<ResolveConflictsResult> ResolveConflictsAsync(long deviceId, long sessionId, ResolveConflictsRequest request, CancellationToken ct = default);
    Task<Stream> DownloadSongAsync(long songId, CancellationToken ct = default);
    Task<SyncActionCounts> ReportSyncErrorAsync(long deviceId, long sessionId, ReportSyncErrorCliRequest request, CancellationToken ct = default);
}