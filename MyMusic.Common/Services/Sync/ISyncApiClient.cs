namespace MyMusic.Common.Services.Sync;

using MyMusic.Common.Services.Sync.Types;

public interface ISyncApiClient
{
    Task<StartSyncResult> StartSyncAsync(long deviceId, StartSyncRequest request, CancellationToken ct = default);
    Task<CheckSyncResult> CheckSyncAsync(long deviceId, CheckSyncRequest request, CancellationToken ct = default);
    Task<UploadFileResult> UploadFileAsync(long deviceId, UploadFileRequest request, CancellationToken ct = default);
    Task RecordChunkAsync(long deviceId, long sessionId, RecordChunkRequest request, CancellationToken ct = default);
    Task<CompleteSyncResult> CompleteSyncAsync(long deviceId, long sessionId, CompleteSyncRequest request, CancellationToken ct = default);
    Task<GetPendingActionsResult> GetPendingActionsAsync(long deviceId, CancellationToken ct = default);
    Task AcknowledgeActionAsync(long deviceId, AcknowledgeActionRequest request, CancellationToken ct = default);
    Task<ResolveConflictsResult> ResolveConflictsAsync(long deviceId, ResolveConflictsRequest request, CancellationToken ct = default);
    Task<Stream> DownloadSongAsync(long songId, CancellationToken ct = default);
}
