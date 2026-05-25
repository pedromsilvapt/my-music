namespace MyMusic.Server.DTO.Sync;

public record SyncCancelResponse
{
    public required long SessionId { get; init; }
    public required bool StagingDirectoryDeleted { get; init; }
}