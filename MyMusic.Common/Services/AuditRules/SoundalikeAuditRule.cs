using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public enum SecondaryAction
{
    Delete = 0,
    Merge = 1,
    Keep = 2
}

public record SoundalikeGroupData
{
    public List<long> SongIds { get; init; } = [];
    public double MatchScore { get; init; }
    public Dictionary<string, double> PairwiseScores { get; init; } = new();
    public string Signature { get; init; } = "";
    public long? PrimarySongId { get; init; }
    public Dictionary<long, SecondaryAction> SecondaryActions { get; init; } = new();
}

public class SoundalikeAuditRule(
    AcousticFingerprintService fingerprintService,
    IFpcalcService fpcalc,
    IOptions<AuditConfig> config,
    ILogger<SoundalikeAuditRule> logger) : IAuditRule
{
    public long Id => 9;
    public string Name => "Duplicate Songs (Soundalike)";
    public string Icon => "copy";
    public string Description => "Detects duplicate songs using acoustic fingerprints";
    public string? CustomPage => "soundalike";

    public async IAsyncEnumerable<AuditNonConformity> Scan(
        MusicDbContext db,
        long ownerId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        logger.LogDebug("SoundalikeAuditRule.Scan called for owner {OwnerId}, fpcalc available: {IsAvailable}", ownerId, fpcalc.IsAvailable());

        if (!fpcalc.IsAvailable())
        {
            logger.LogWarning("Fpcalc not available, skipping soundalike scan for user {OwnerId}", ownerId);
            yield break;
        }

        var auditConfig = config.Value;
        var lookupThreshold = auditConfig.SoundalikeLookupThreshold;
        var matchThreshold = auditConfig.SoundalikeMatchThreshold;

        logger.LogDebug("Starting FindDuplicatesWithScoresAsync for owner {OwnerId} with lookup={Lookup}, match={Match}", ownerId, lookupThreshold, matchThreshold);

        var existingNonConformities = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        var existingBySignature = new Dictionary<string, AuditNonConformity>();
        foreach (var nc in existingNonConformities)
        {
            if (nc.Data == null) continue;
            try
            {
                var existingData = JsonSerializer.Deserialize<SoundalikeGroupData>(nc.Data.Value);
                if (existingData?.Signature != null)
                {
                    existingBySignature[existingData.Signature] = nc;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize existing soundalike non-conformity {Id}", nc.Id);
            }
        }

        await foreach (var (group, pairwiseScores) in
            fingerprintService.FindDuplicatesWithScoresAsync(ownerId, lookupThreshold, matchThreshold, cancellationToken))
        {
            logger.LogDebug("Found group with {Count} songs", group.Count);

            if (group.Count < 2)
                continue;

            var minScore = pairwiseScores.Count > 0
                ? pairwiseScores.Values.Min()
                : 1.0;

            var signature = string.Join("-", group.Select(s => s.Id).Order());

            var data = new SoundalikeGroupData
            {
                SongIds = group.Select(s => s.Id).ToList(),
                MatchScore = minScore,
                PairwiseScores = pairwiseScores,
                Signature = signature
            };

            if (existingBySignature.TryGetValue(signature, out var existing))
            {
                SoundalikeGroupData? existingData = null;
                try
                {
                    existingData = JsonSerializer.Deserialize<SoundalikeGroupData>(existing.Data!.Value);
                }
                catch (JsonException) { }

                logger.LogDebug("Updating existing non-conformity {Id} for signature {Signature}", existing.Id, signature);
                existing.Data = JsonSerializer.SerializeToElement(data with
                {
                    PrimarySongId = existingData?.PrimarySongId,
                    SecondaryActions = existingData?.SecondaryActions ?? new Dictionary<long, SecondaryAction>()
                });
                db.AuditNonConformities.Update(existing);
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            yield return new AuditNonConformity
            {
                AuditRuleId = Id,
                OwnerId = ownerId,
                Data = JsonSerializer.SerializeToElement(data),
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    public Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Soundalike rule does not support individual song patching. Use the custom page to manage duplicates.");
    }
}
