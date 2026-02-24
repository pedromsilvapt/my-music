using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Audits;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("audits")]
public class AuditsController(
    ICurrentUser currentUser,
    IAuditService auditService) : ControllerBase
{
    [HttpGet("rules", Name = "ListAuditRules")]
    public async Task<ListAuditRulesResponse> ListRules(
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var rules = auditService.GetRules();
        var ruleItems = new List<ListAuditRulesItem>();

        foreach (var rule in rules)
        {
            var count = await auditService.GetNonConformityCount(db, rule.Id, currentUser.Id, cancellationToken);
            ruleItems.Add(new ListAuditRulesItem
            {
                Id = rule.Id,
                Name = rule.Name,
                Icon = rule.Icon,
                Description = rule.Description,
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
            NonConformities = nonConformities.Select(ListAuditNonConformitiesItem.FromEntity),
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
}