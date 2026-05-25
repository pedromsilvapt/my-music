using MyMusic.CLI.Services.Sync.Types;

namespace MyMusic.CLI.Api.Dtos;

public record ListSyncRecordsResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
    public int TotalCount { get; init; }
}

public record SyncRecordSongInfo
{
    public required long Id { get; init; }
    public required string Title { get; init; }
    public required string ArtistNames { get; init; }
    public string? CoverId { get; init; }
}

public record SyncRecordResponseItem
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
        SyncRecordAction.CreateLocal or SyncRecordAction.UpdateLocal or SyncRecordAction.Unlink or SyncRecordAction.Rename or SyncRecordAction.Delete => "Client",
        SyncRecordAction.CreateRemote or SyncRecordAction.UpdateRemote or SyncRecordAction.Link or SyncRecordAction.Skipped or SyncRecordAction.Conflict or SyncRecordAction.UpdateTimestamp or SyncRecordAction.Error => "Server",
        _ => throw new InvalidOperationException($"Unknown sync record action: {Action}")
    };
}