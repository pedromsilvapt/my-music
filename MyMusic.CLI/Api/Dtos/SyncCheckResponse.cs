namespace MyMusic.CLI.Api.Dtos;

public record SyncCheckResponse
{
    public required List<SyncFileInfoItem> ToCreate { get; init; }
    public required List<SyncFileInfoItem> ToUpdate { get; init; }
}