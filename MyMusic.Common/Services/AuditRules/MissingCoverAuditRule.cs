using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingCoverAuditRule : IAuditRule
{
    public long Id => 1;
    public string Name => "Missing Cover";
    public string Icon => "IconPhotoOff";
    public string Description => "Songs that do not have an associated cover image.";

    public async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songsWithoutCover = await db.Songs
            .Where(s => s.OwnerId == ownerId && s.CoverId == null && !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var nonConformities = songsWithoutCover.Select(songId => new AuditNonConformity
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
        throw new NotImplementedException("Automatic cover extraction is not yet implemented.");
}