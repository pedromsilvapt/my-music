namespace MyMusic.CLI.Api.Dtos;

public record SyncStartResponse
{
    public required long SessionId { get; init; }
}