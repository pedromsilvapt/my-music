namespace MyMusic.CLI.Api.Dtos;

public record SyncCommitRequest
{
    public string? Direction { get; init; }
}