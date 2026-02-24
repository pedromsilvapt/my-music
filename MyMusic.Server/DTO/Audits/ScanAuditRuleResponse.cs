namespace MyMusic.Server.DTO.Audits;

public record ScanAuditRuleResponse
{
    public required int NonConformitiesCreated { get; set; }
}