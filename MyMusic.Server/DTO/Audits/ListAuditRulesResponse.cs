namespace MyMusic.Server.DTO.Audits;

public record ListAuditRulesResponse
{
    public required IEnumerable<ListAuditRuleItem> Rules { get; set; }
}

public record ListAuditRuleItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Description { get; set; }
    public string? CustomPageRoute { get; set; }
    public required int NonConformityCount { get; set; }
}