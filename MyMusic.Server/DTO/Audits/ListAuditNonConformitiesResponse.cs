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
    public required long SongId { get; set; }
    public required ListSongItem Song { get; set; }
    public required bool HasWaiver { get; set; }
    public string? WaiverReason { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static ListAuditNonConformityItem FromEntity(AuditNonConformity nc) =>
        new()
        {
            Id = nc.Id,
            SongId = nc.SongId,
            Song = ListSongItem.FromEntity(nc.Song),
            HasWaiver = nc.HasWaiver,
            WaiverReason = nc.WaiverReason,
            CreatedAt = nc.CreatedAt,
        };
}