using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class NonJpegCoverAuditRule : IAuditRule
{
    public long Id => 7;
    public string Name => "Non-JPEG Covers";
    public string Icon => "IconFileType";
    public string Description => "Songs with cover artwork in a format other than JPEG.";

    public async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songsWithNonJpegCover = await db.Songs
            .Where(s => s.OwnerId == ownerId
                        && s.CoverId != null
                        && s.Cover != null
                        && s.Cover.MimeType != "image/jpeg"
                        && !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var nonConformities = songsWithNonJpegCover.Select(songId => new AuditNonConformity
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
        throw new NotImplementedException("Automatic cover conversion to JPEG is not yet implemented.");
}