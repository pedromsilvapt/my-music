using System.Text.Json;
using System.Text.Json.Serialization;
using MyMusic.CLI.Api.Dtos;

namespace MyMusic.CLI.Api;

/// <summary>
/// Source-generated JSON metadata for every type sent to or received from the server (required for Native AOT).
/// Every request/response type of <see cref="IMyMusicClient"/> must be listed here; <c>CliJsonContextTests</c> enforces it.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AcknowledgeActionRequest))]
[JsonSerializable(typeof(AcknowledgeActionResponse))]
[JsonSerializable(typeof(CreateDeviceRequest))]
[JsonSerializable(typeof(CreateDeviceResponse))]
[JsonSerializable(typeof(CreatePendingActionsResponse))]
[JsonSerializable(typeof(DeleteSessionResponse))]
[JsonSerializable(typeof(GetDeviceSongsResponse))]
[JsonSerializable(typeof(ListDevicesResponse))]
[JsonSerializable(typeof(ListSyncRecordsResponse))]
[JsonSerializable(typeof(ListSyncSessionsResponse))]
[JsonSerializable(typeof(PruneSessionsRequest))]
[JsonSerializable(typeof(PruneSessionsResponse))]
[JsonSerializable(typeof(ReportSyncErrorRequest))]
[JsonSerializable(typeof(ReportSyncErrorResponse))]
[JsonSerializable(typeof(SyncCheckRequest))]
[JsonSerializable(typeof(SyncCheckResponse))]
[JsonSerializable(typeof(SyncCommitResponse))]
[JsonSerializable(typeof(SyncCompleteResponse))]
[JsonSerializable(typeof(SyncDeduplicatePrepareResponse))]
[JsonSerializable(typeof(SyncResolveConflictsRequest))]
[JsonSerializable(typeof(SyncResolveConflictsResponse))]
[JsonSerializable(typeof(SyncStartRequest))]
[JsonSerializable(typeof(SyncStartResponse))]
[JsonSerializable(typeof(SyncUploadResponse))]
[JsonSerializable(typeof(UpdateDeviceRequest))]
[JsonSerializable(typeof(UpdateDeviceResponse))]
public partial class CliJsonContext : JsonSerializerContext;
