using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

/// <summary>
/// Base class for audit rules that provides common scanning logic.
/// Implementations only need to define the filtering predicate for songs that violate the rule.
/// </summary>
public abstract class AuditRuleBase : IAuditRule
{
    public abstract long Id { get; }
    public abstract string Name { get; }
    public abstract string Icon { get; }
    public abstract string Description { get; }

    /// <summary>
    /// Returns the predicate that identifies songs violating this audit rule.
    /// This predicate should NOT include owner filtering - that is handled by the base class.
    /// </summary>
    protected abstract Expression<Func<Song, bool>> GetViolationPredicate();

    public virtual async Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default)
    {
        // Step 1: Get existing non-conformities for this rule and owner
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        // Step 2: Query songs matching the violation predicate, excluding already tracked
        var violationPredicate = GetViolationPredicate();
        var newViolations = await db.Songs
            .Where(s => s.OwnerId == ownerId)
            .Where(violationPredicate)
            .Where(s => !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        // Step 3: Create AuditNonConformity entities
        var nonConformities = newViolations.Select(songId => new AuditNonConformity
        {
            SongId = songId,
            AuditRuleId = Id,
            OwnerId = ownerId,
            HasWaiver = false,
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        // Step 4: Persist and return count
        db.AuditNonConformities.AddRange(nonConformities);
        await db.SaveChangesAsync(cancellationToken);

        return nonConformities.Count;
    }

    public abstract Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default);
}
