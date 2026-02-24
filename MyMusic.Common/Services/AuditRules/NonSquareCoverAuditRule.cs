using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class NonSquareCoverAuditRule : IAuditRule
{
    public long Id => 8;
    public string Name => "Non-Square Covers";
    public string Icon => "IconAspectRatio";
    public string Description => "Songs with cover artwork not in 1:1 aspect ratio.";

    public async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songsWithNonSquareCover = await db.Songs
            .Where(s => s.OwnerId == ownerId
                        && s.CoverId != null
                        && s.Cover != null
                        && s.Cover.Width != s.Cover.Height
                        && !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var nonConformities = songsWithNonSquareCover.Select(songId => new AuditNonConformity
        {
            SongId = songId,
            AuditRuleId = Id,
            OwnerId = ownerId,
            HasWaiver = false,
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        db.AuditNonConformities.AddRange(nonConformities);
        await db.SaveChangesAsync(cancellationToken);

        return nonConformities.Count;
    }

    public Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic cover resizing to square is not yet implemented.");
}