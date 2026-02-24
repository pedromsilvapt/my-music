using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingGenresAuditRule : IAuditRule
{
    public long Id => 3;
    public string Name => "Missing Genres";
    public string Icon => "IconTagOff";
    public string Description => "Songs that do not have any genres assigned.";

    public async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songsWithoutGenres = await db.Songs
            .Where(s => s.OwnerId == ownerId && s.Genres.Count == 0 && !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var nonConformities = songsWithoutGenres.Select(songId => new AuditNonConformity
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
        throw new NotImplementedException("Automatic genre detection is not yet implemented.");
}