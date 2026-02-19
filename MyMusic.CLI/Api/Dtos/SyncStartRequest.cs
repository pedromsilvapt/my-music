namespace MyMusic.CLI.Api.Dtos;

public record SyncStartRequest
{
    public bool DryRun { get; init; }
}