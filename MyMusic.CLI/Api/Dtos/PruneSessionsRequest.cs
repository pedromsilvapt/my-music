namespace MyMusic.CLI.Api.Dtos;

public record PruneSessionsRequest
{
    public bool All { get; init; }
}