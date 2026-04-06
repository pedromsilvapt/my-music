using System.Text.Json;
using MyMusic.Common.Entities;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.DTO.Audits;

public record ListAuditNonConformitiesResponse
{
    public required IEnumerable<ListAuditNonConformityItem> NonConformities { get; set; }
}

public record ListAuditNonConformityItem
{
    public required long Id { get; set; }
    public long? SongId { get; set; }
    public ListSongItem? Song { get; set; }
    public JsonElement? Data { get; set; }
    public required bool HasWaiver { get; set; }
    public string? WaiverReason { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static ListAuditNonConformityItem FromEntity(AuditNonConformity nc) =>
        new()
        {
            Id = nc.Id,
            SongId = nc.SongId,
            Song = nc.Song != null ? ListSongItem.FromEntity(nc.Song) : null,
            Data = nc.Data,
            HasWaiver = nc.HasWaiver,
            WaiverReason = nc.WaiverReason,
            CreatedAt = nc.CreatedAt,
        };
}