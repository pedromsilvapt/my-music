namespace MyMusic.Server.DTO.Sync;

public record GetSyncSessionResponse
{
    public required SyncSessionItem Session { get; init; }
}
