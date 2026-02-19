namespace MyMusic.CLI.Api.Dtos;

public record SyncCheckRequest
{
    public required List<SyncFileInfoItem> Files { get; init; }
    public bool Force { get; init; }
}