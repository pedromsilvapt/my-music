using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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
    public virtual string? CustomPageRoute => null;

    /// <summary>
    /// Returns the predicate that identifies songs violating this audit rule.
    /// This predicate should NOT include owner filtering - that is handled by the base class.
    /// </summary>
    protected abstract Expression<Func<Song, bool>> GetViolationPredicate();

    public virtual async IAsyncEnumerable<AuditNonConformity> Scan(
        MusicDbContext db,
        long ownerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var violationPredicate = GetViolationPredicate();
        var newViolations = await db.Songs
            .Where(s => s.OwnerId == ownerId)
            .Where(violationPredicate)
            .Where(s => !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        foreach (var songId in newViolations)
        {
            yield return new AuditNonConformity
            {
                SongId = songId,
                AuditRuleId = Id,
                OwnerId = ownerId,
                HasWaiver = false,
                CreatedAt = DateTime.UtcNow,
            };
        }
    }

    public abstract Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default);
}
