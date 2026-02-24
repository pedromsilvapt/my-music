namespace MyMusic.Server.DTO.Audits;

public record GetAuditRuleResponse
{
    public required GetAuditRuleItem Rule { get; set; }
}

public record GetAuditRuleItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Description { get; set; }
    public required int NonConformityCount { get; set; }
}