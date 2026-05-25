using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record SyncUploadResponse
{
    public required bool Success { get; init; }
    public long? SongId { get; init; }
    public long? RecordId { get; init; }
    public string? Action { get; init; }
    public System.Text.Json.JsonElement? Data { get; init; }
    public required SyncActionCounts Counts { get; init; }
}