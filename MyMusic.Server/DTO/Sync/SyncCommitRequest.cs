namespace MyMusic.Server.DTO.Sync;

public record SyncCommitRequest
{
    public string? Direction { get; init; }
}