namespace MyMusic.CLI.Api.Dtos;

public record PruneSessionsResponse
{
    public required int DeletedCount { get; init; }
}