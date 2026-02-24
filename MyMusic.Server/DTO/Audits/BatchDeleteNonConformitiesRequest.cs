namespace MyMusic.Server.DTO.Audits;

public record BatchDeleteNonConformitiesRequest
{
    public required List<long> Ids { get; init; }
}