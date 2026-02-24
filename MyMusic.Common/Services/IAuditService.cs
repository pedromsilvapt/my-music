using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public interface IAuditService
{
    IEnumerable<IAuditRule> GetRules();
    IAuditRule? GetRule(long ruleId);
    Task<int> ScanRule(MusicDbContext db, long ruleId, long ownerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditNonConformity>> GetNonConformities(MusicDbContext db, long ruleId, long ownerId,
        CancellationToken cancellationToken = default);

    Task<int> GetNonConformityCount(MusicDbContext db, long ruleId, long ownerId,
        CancellationToken cancellationToken = default);

    Task SetWaiver(MusicDbContext db, long nonConformityId, long ownerId, bool hasWaiver, string? waiverReason,
        CancellationToken cancellationToken = default);

    Task SetWaiverBatch(MusicDbContext db, List<long> ids, long ownerId, bool hasWaiver, string? waiverReason,
        CancellationToken cancellationToken = default);

    Task DeleteNonConformitiesBatch(MusicDbContext db, List<long> ids, long ownerId,
        CancellationToken cancellationToken = default);
}