using MyMusic.CLI.Api.Dtos;
using Refit;

namespace MyMusic.CLI.Api;

public interface IMyMusicClient
{
    [Post("/api/devices")]
    Task<CreateDeviceResponse> CreateDeviceAsync(
        [Body] CreateDeviceRequest request,
        CancellationToken ct = default);

    [Put("/api/devices/{deviceId}")]
    Task<UpdateDeviceResponse> UpdateDeviceAsync(
        long deviceId,
        [Body] UpdateDeviceRequest request,
        CancellationToken ct = default);

    [Get("/api/devices")]
    Task<ListDevicesResponse> GetDevicesAsync(
        CancellationToken ct = default);

    [Get("/api/devices/{deviceId}/sessions")]
    Task<ListSyncSessionsResponse> GetSessionsAsync(
        long deviceId,
        [Query] int count,
        CancellationToken ct = default);

    [Get("/api/devices/{deviceId}/sessions/{sessionId}/records")]
    Task<ListSyncRecordsResponse> GetSessionRecordsAsync(
        long deviceId,
        long sessionId,
        [Query] string? actions,
        [Query] string? source,
        CancellationToken ct = default);

    [Post("/api/devices/{deviceId}/sync/start")]
    Task<SyncStartResponse> StartSyncAsync(
        long deviceId,
        [Body] SyncStartRequest request,
        CancellationToken ct = default);

    [Post("/api/devices/{deviceId}/sync/{sessionId}/records")]
    Task<SyncRecordsResponse> RecordChunkAsync(
        long deviceId,
        long sessionId,
        [Body] SyncRecordsRequest request,
        CancellationToken ct = default);

    [Post("/api/devices/{deviceId}/sync/{sessionId}/complete")]
    Task<SyncCompleteResponse> CompleteSyncAsync(
        long deviceId,
        long sessionId,
        CancellationToken ct = default);

    [Post("/api/devices/{deviceId}/sync/check")]
    Task<SyncCheckResponse> CheckSyncAsync(
        long deviceId,
        [Body] SyncCheckRequest request,
        CancellationToken ct = default);

    [Multipart]
    [Post("/api/devices/{deviceId}/sync/upload")]
    Task<SyncUploadResponse> UploadFileAsync(
        long deviceId,
        [AliasAs("file")] StreamPart file,
        [AliasAs("path")] string path,
        [AliasAs("modifiedAt")] string modifiedAt,
        [AliasAs("createdAt")] string createdAt,
        CancellationToken ct = default);

    [Get("/api/devices/{deviceId}/sync/pending-actions")]
    Task<GetPendingActionsResponse> GetPendingActionsAsync(
        long deviceId,
        CancellationToken ct = default);

    [Post("/api/devices/{deviceId}/sync/acknowledge")]
    Task<AcknowledgeActionResponse> AcknowledgeActionAsync(
        long deviceId,
        [Body] AcknowledgeActionRequest request,
        CancellationToken ct = default);

    [Get("/songs/{songId}/download")]
    Task<Stream> DownloadSongAsync(
        long songId,
        CancellationToken ct = default);
}