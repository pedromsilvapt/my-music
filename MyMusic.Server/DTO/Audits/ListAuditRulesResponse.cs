namespace MyMusic.Server.DTO.Audits;

public record ListAuditRulesResponse
{
    public required IEnumerable<ListAuditRulesItem> Rules { get; set; }
}

public record ListAuditRulesItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Description { get; set; }
    public required int NonConformityCount { get; set; }
}