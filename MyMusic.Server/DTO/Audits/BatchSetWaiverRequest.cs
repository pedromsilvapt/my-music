namespace MyMusic.Server.DTO.Audits;

public record BatchSetWaiverRequest
{
    public required List<long> Ids { get; init; }
    public required bool HasWaiver { get; init; }
    public string? WaiverReason { get; init; }
}