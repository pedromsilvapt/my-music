using MyMusic.Common.Entities;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.DTO.Audits;

public record ListAuditNonConformitiesResponse
{
    public required IEnumerable<ListAuditNonConformitiesItem> NonConformities { get; set; }
}

public record ListAuditNonConformitiesItem
{
    public required long Id { get; set; }
    public required long SongId { get; set; }
    public required ListSongsItem Song { get; set; }
    public required bool HasWaiver { get; set; }
    public string? WaiverReason { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static ListAuditNonConformitiesItem FromEntity(AuditNonConformity nc) =>
        new()
        {
            Id = nc.Id,
            SongId = nc.SongId,
            Song = ListSongsItem.FromEntity(nc.Song),
            HasWaiver = nc.HasWaiver,
            WaiverReason = nc.WaiverReason,
            CreatedAt = nc.CreatedAt,
        };
}