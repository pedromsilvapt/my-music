using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingYearAuditRule : IAuditRule
{
    public long Id => 2;
    public string Name => "Missing Year";
    public string Icon => "IconCalendarOff";
    public string Description => "Songs that do not have a release year.";

    public async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songsWithoutYear = await db.Songs
            .Where(s => s.OwnerId == ownerId && s.Year == null && !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var nonConformities = songsWithoutYear.Select(songId => new AuditNonConformity
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
        throw new NotImplementedException("Automatic year detection is not yet implemented.");
}