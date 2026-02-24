using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public class AuditService(IEnumerable<IAuditRule> rules) : IAuditService
{
    private readonly Dictionary<long, IAuditRule> _rules = rules.ToDictionary(r => r.Id);

    public IEnumerable<IAuditRule> GetRules() => _rules.Values;

    public IAuditRule? GetRule(long ruleId) => _rules.GetValueOrDefault(ruleId);

    public async Task<int> ScanRule(MusicDbContext db, long ruleId, long ownerId,
        CancellationToken cancellationToken = default)
    {
        var rule = GetRule(ruleId) ?? throw new Exception($"Audit rule not found with id {ruleId}");
        return await rule.Scan(db, ownerId, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditNonConformity>> GetNonConformities(
        MusicDbContext db,
        long ruleId,
        long ownerId,
        CancellationToken cancellationToken = default)
    {
        return await db.AuditNonConformities
            .Include(nc => nc.Song)
            .ThenInclude(s => s.Album)
            .Include(nc => nc.Song)
            .ThenInclude(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(nc => nc.Song)
            .ThenInclude(s => s.Genres)
            .ThenInclude(sg => sg.Genre)
            .Where(nc => nc.AuditRuleId == ruleId && nc.OwnerId == ownerId)
            .OrderByDescending(nc => nc.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNonConformityCount(
        MusicDbContext db,
        long ruleId,
        long ownerId,
        CancellationToken cancellationToken = default)
    {
        return await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == ruleId && nc.OwnerId == ownerId && !nc.HasWaiver)
            .CountAsync(cancellationToken);
    }

    public async Task SetWaiver(
        MusicDbContext db,
        long nonConformityId,
        long ownerId,
        bool hasWaiver,
        string? waiverReason,
        CancellationToken cancellationToken = default)
    {
        var nonConformity = await db.AuditNonConformities
                                .FirstOrDefaultAsync(nc => nc.Id == nonConformityId && nc.OwnerId == ownerId,
                                    cancellationToken)
                            ?? throw new Exception($"Audit non-conformity not found with id {nonConformityId}");

        nonConformity.HasWaiver = hasWaiver;
        nonConformity.WaiverReason = hasWaiver ? waiverReason : null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetWaiverBatch(
        MusicDbContext db,
        List<long> ids,
        long ownerId,
        bool hasWaiver,
        string? waiverReason,
        CancellationToken cancellationToken = default)
    {
        var nonConformities = await db.AuditNonConformities
            .Where(nc => ids.Contains(nc.Id) && nc.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        foreach (var nc in nonConformities)
        {
            nc.HasWaiver = hasWaiver;
            nc.WaiverReason = hasWaiver ? waiverReason : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteNonConformitiesBatch(
        MusicDbContext db,
        List<long> ids,
        long ownerId,
        CancellationToken cancellationToken = default)
    {
        var nonConformities = await db.AuditNonConformities
            .Where(nc => ids.Contains(nc.Id) && nc.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        db.AuditNonConformities.RemoveRange(nonConformities);
        await db.SaveChangesAsync(cancellationToken);
    }
}