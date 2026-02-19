namespace MyMusic.Server.DTO.Sync;

public record SyncCheckRequest
{
    public required List<SyncFileInfoItem> Files { get; init; }
    public bool Force { get; init; }
}