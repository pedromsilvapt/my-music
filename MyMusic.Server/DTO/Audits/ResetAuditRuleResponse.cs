namespace MyMusic.Server.DTO.Audits;

public record ResetAuditRuleResponse
{
    public required int DeletedCount { get; set; }
}
