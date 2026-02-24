using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MediumCoverAuditRule(IOptions<AuditConfig> config) : IAuditRule
{
    public long Id => 5;
    public string Name => "Medium Sized Covers";
    public string Icon => "IconPhotoDown";

    public string Description =>
        $"Songs with cover artwork smaller than {config.Value.MediumCoverThreshold} pixels (on both dimensions).";

    public async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        var threshold = config.Value.MediumCoverThreshold;
        var smallThreshold = config.Value.SmallCoverThreshold;

        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songsWithMediumCover = await db.Songs
            .Where(s => s.OwnerId == ownerId
                        && s.CoverId != null
                        && s.Cover != null
                        && s.Cover.Width >= smallThreshold
                        && s.Cover.Width < threshold
                        && s.Cover.Height >= smallThreshold
                        && s.Cover.Height < threshold
                        && !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var nonConformities = songsWithMediumCover.Select(songId => new AuditNonConformity
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
        throw new NotImplementedException("Automatic cover upscaling is not yet implemented.");
}