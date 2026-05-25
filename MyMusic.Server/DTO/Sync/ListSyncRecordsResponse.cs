using System.Text.Json;
using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

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
    public JsonElement? Data { get; init; }
    public long? ResolvesConflictRecordId { get; init; }
    public SyncRecordSongInfo? SongInfo { get; init; }
    public string? Reason { get; init; }
    public bool Acknowledged { get; init; }
    public DateTime ProcessedAt { get; init; }

    public static SyncRecordResponseItem FromEntity(DeviceSyncSessionRecord record, bool includeSongInfo = false)
    {
        var item = new SyncRecordResponseItem
        {
            Id = record.Id,
            FilePath = record.FilePath,
            Action = record.Action,
            SongId = record.SongId,
            Data = record.Data,
            ResolvesConflictRecordId = record.ResolvesConflictRecordId,
            Reason = record.Reason,
            Acknowledged = record.Acknowledged,
            ProcessedAt = record.ProcessedAt,
        };

        if (includeSongInfo && record.SongId.HasValue && record.Song is not null)
        {
            item = item with
            {
                SongInfo = new SyncRecordSongInfo
                {
                    Id = record.Song.Id,
                    Title = record.Song.Title,
                    ArtistNames = string.Join(", ", record.Song.Artists.Select(a => a.Artist.Name)),
                    CoverId = record.Song.CoverId?.ToString(),
                }
            };
        }

        return item;
    }
}