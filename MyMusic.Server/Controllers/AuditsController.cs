using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using MyMusic.Server.DTO.Audits;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("audits")]
public class AuditsController(
    ICurrentUser currentUser,
    IAuditService auditService,
    AcousticFingerprintService fingerprintService,
    ISoundalikeResolutionService resolutionService) : ControllerBase
{
    [HttpGet("rules", Name = "ListAuditRules")]
    public async Task<ListAuditRulesResponse> ListRules(
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var rules = auditService.GetRules();
        var ruleItems = new List<ListAuditRuleItem>();

        foreach (var rule in rules)
        {
            var count = await auditService.GetNonConformityCount(db, rule.Id, currentUser.Id, cancellationToken);
            ruleItems.Add(new ListAuditRuleItem
            {
                Id = rule.Id,
                Name = rule.Name,
                Icon = rule.Icon,
                Description = rule.Description,
                CustomPage = rule.CustomPage,
                NonConformityCount = count,
            });
        }

        return new ListAuditRulesResponse { Rules = ruleItems };
    }

    [HttpGet("rules/{id:long}", Name = "GetAuditRule")]
    public async Task<GetAuditRuleResponse> GetRule(
        [FromRoute] long id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var rule = auditService.GetRule(id)
                   ?? throw new Exception($"Audit rule not found with id {id}");

        var count = await auditService.GetNonConformityCount(db, rule.Id, currentUser.Id, cancellationToken);

        return new GetAuditRuleResponse
        {
            Rule = new GetAuditRuleItem
            {
                Id = rule.Id,
                Name = rule.Name,
                Icon = rule.Icon,
                Description = rule.Description,
                CustomPage = rule.CustomPage,
                NonConformityCount = count,
            },
        };
    }

    [HttpPost("rules/{id:long}/scan", Name = "ScanAuditRule")]
    public async Task<ScanAuditRuleResponse> ScanRule(
        [FromRoute] long id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var count = await auditService.ScanRule(db, id, currentUser.Id, cancellationToken);
        return new ScanAuditRuleResponse { NonConformitiesCreated = count };
    }

    [HttpGet("rules/{id:long}/non-conformities", Name = "ListAuditNonConformities")]
    public async Task<ListAuditNonConformitiesResponse> ListNonConformities(
        [FromRoute] long id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var nonConformities = await auditService.GetNonConformities(db, id, currentUser.Id, cancellationToken);
        return new ListAuditNonConformitiesResponse
        {
            NonConformities = nonConformities.Select(ListAuditNonConformityItem.FromEntity),
        };
    }

    [HttpPost("non-conformities/{id:long}/waiver", Name = "SetAuditWaiver")]
    public async Task SetWaiver(
        [FromRoute] long id,
        [FromBody] SetWaiverRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        await auditService.SetWaiver(db, id, currentUser.Id, request.HasWaiver, request.WaiverReason,
            cancellationToken);
    }

    [HttpDelete("non-conformities/{id:long}", Name = "DeleteAuditNonConformity")]
    public async Task DeleteNonConformity(
        [FromRoute] long id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var nonConformity = await db.AuditNonConformities
                                .FirstOrDefaultAsync(nc => nc.Id == id && nc.OwnerId == currentUser.Id,
                                    cancellationToken)
                            ?? throw new Exception($"Audit non-conformity not found with id {id}");

        db.AuditNonConformities.Remove(nonConformity);
        await db.SaveChangesAsync(cancellationToken);
    }

    [HttpPost("non-conformities/waiver/batch", Name = "BatchSetAuditWaiver")]
    public async Task BatchSetWaiver(
        [FromBody] BatchSetWaiverRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        await auditService.SetWaiverBatch(db, request.Ids, currentUser.Id, request.HasWaiver, request.WaiverReason,
            cancellationToken);
    }

    [HttpPost("non-conformities/batch-delete", Name = "BatchDeleteAuditNonConformities")]
    public async Task BatchDeleteNonConformities(
        [FromBody] BatchDeleteNonConformitiesRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        await auditService.DeleteNonConformitiesBatch(db, request.Ids, currentUser.Id, cancellationToken);
    }

    [HttpGet("soundalike", Name = "GetSoundalikeDuplicates")]
    public async Task<ActionResult<GetSoundalikeDuplicatesResponse>> GetSoundalikeDuplicates(
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        const long soundalikeRuleId = 9;
        var ownerId = currentUser.Id;

        var nonConformities = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == soundalikeRuleId && nc.OwnerId == ownerId)
            .OrderByDescending(nc => nc.CreatedAt)
            .ToListAsync(cancellationToken);

        var groups = new List<SoundalikeDuplicateGroup>();

        foreach (var nc in nonConformities)
        {
            if (nc.Data == null)
                continue;

            var data = nc.Data.Value.Deserialize<SoundalikeGroupData>();
            if (data == null || data.SongIds.Count == 0)
                continue;

            var songs = await db.Songs
                .Where(s => data.SongIds.Contains(s.Id))
                .Include(s => s.Album)
                .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
                .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Cover)
                .Include(s => s.Devices).ThenInclude(sd => sd.Device)
                .ToListAsync(cancellationToken);

            if (songs.Count < 2)
                continue;

            groups.Add(new SoundalikeDuplicateGroup
            {
                NonConformityId = nc.Id,
                CreatedAt = nc.CreatedAt,
                MatchScore = data.MatchScore,
                Songs = songs.Select(SoundalikeSongItem.FromEntity).ToList(),
                PrimarySongId = data.PrimarySongId,
                SecondaryActions = data.SecondaryActions
            });
        }

        return Ok(new GetSoundalikeDuplicatesResponse { Groups = groups });
    }

    [HttpPatch("soundalike/{nonConformityId:long}/selection", Name = "UpdateSoundalikeSelection")]
    public async Task<ActionResult> UpdateSoundalikeSelection(
        [FromRoute] long nonConformityId,
        [FromBody] UpdateSoundalikeSelectionRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.Id;

        var nc = await db.AuditNonConformities
            .FirstOrDefaultAsync(nc => nc.Id == nonConformityId && nc.OwnerId == ownerId, cancellationToken);

        if (nc == null)
            return NotFound();

        if (nc.Data == null)
            return NotFound();

        SoundalikeGroupData data;
        try
        {
            data = nc.Data.Value.Deserialize<SoundalikeGroupData>()
                   ?? throw new JsonException("Deserialization returned null");
        }
        catch (JsonException)
        {
            return NotFound();
        }

        data = data with
        {
            PrimarySongId = request.PrimarySongId,
            SecondaryActions = request.SecondaryActions
        };

        nc.Data = JsonSerializer.SerializeToElement(data);
        db.AuditNonConformities.Update(nc);
        await db.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpPost("soundalike/exclude", Name = "ExcludeDuplicatePair")]
    public async Task<ActionResult> ExcludeDuplicatePair(
        [FromBody] ExcludeDuplicatePairRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.Id;

        await fingerprintService.ExcludePairAsync(
            request.SongAId,
            request.SongBId,
            ownerId,
            request.Reason,
            cancellationToken);

        var (aId, bId) = request.SongAId < request.SongBId
            ? (request.SongAId, request.SongBId)
            : (request.SongBId, request.SongAId);

        var nonConformities = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == 9 && nc.OwnerId == ownerId && nc.Data != null)
            .ToListAsync(cancellationToken);

        foreach (var nc in nonConformities)
        {
            var data = nc.Data!.Value.Deserialize<SoundalikeGroupData>();
            if (data == null)
                continue;

            var key = $"{aId}-{bId}";
            if (data.PairwiseScores.ContainsKey(key) || 
                (data.SongIds.Contains(aId) && data.SongIds.Contains(bId)))
            {
                db.AuditNonConformities.Remove(nc);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("soundalike/excluded", Name = "ListExcludedPairs")]
    public async Task<ActionResult<ListExcludedPairsResponse>> ListExcludedPairs(
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.Id;

        var pairs = await fingerprintService.GetExcludedPairsAsync(ownerId, cancellationToken);

        var items = new List<ExcludedPairItem>();
        foreach (var pair in pairs)
        {
            var songA = await db.Songs
                .Include(s => s.Album)
                .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
                .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Devices).ThenInclude(sd => sd.Device)
                .FirstOrDefaultAsync(s => s.Id == pair.SongAId, cancellationToken);

            var songB = await db.Songs
                .Include(s => s.Album)
                .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
                .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Devices).ThenInclude(sd => sd.Device)
                .FirstOrDefaultAsync(s => s.Id == pair.SongBId, cancellationToken);

            if (songA != null && songB != null)
            {
                items.Add(new ExcludedPairItem
                {
                    Id = pair.Id,
                    SongA = ListSongItem.FromEntity(songA),
                    SongB = ListSongItem.FromEntity(songB),
                    CreatedAt = pair.CreatedAt,
                    Reason = pair.Reason
                });
            }
        }

        return Ok(new ListExcludedPairsResponse { Pairs = items });
    }

    [HttpDelete("soundalike/excluded/{id:long}", Name = "RemoveExcludedPair")]
    public async Task<ActionResult> RemoveExcludedPair(
        [FromRoute] long id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.Id;
        var pair = await db.ExcludedDuplicatePairs
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, cancellationToken);

        if (pair == null)
            return NotFound();

        db.ExcludedDuplicatePairs.Remove(pair);
        await db.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpPost("soundalike/resolve", Name = "ResolveSoundalikes")]
    public async Task<ActionResult<ResolveSoundalikesResponse>> ResolveSoundalikes(
        [FromBody] ResolveSoundalikesRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var resolutions = request.Resolutions.Select(r => new GroupResolutionInput
            {
                NonConformityId = r.NonConformityId,
                PrimarySongId = r.PrimarySongId,
                SecondaryActions = r.SecondaryActions.Select(a => new SecondarySongActionInput
                {
                    SongId = a.SongId,
                    Action = a.Action,
                }).ToList(),
            }).ToList();

            var resolvedCount = await resolutionService.ResolveAsync(db, ownerId, resolutions, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Ok(new ResolveSoundalikesResponse { ResolvedCount = resolvedCount });
        }
        catch (UnauthorizedAccessException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Forbid();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}