namespace MyMusic.CLI.Api.Dtos;

public record SyncCompleteRequest
{
    public string? Direction { get; init; }
}