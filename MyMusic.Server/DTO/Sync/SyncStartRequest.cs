namespace MyMusic.Server.DTO.Sync;

public record SyncStartRequest
{
    public bool DryRun { get; init; }
}

public record SyncStartResponse
{
    public required long SessionId { get; init; }
}