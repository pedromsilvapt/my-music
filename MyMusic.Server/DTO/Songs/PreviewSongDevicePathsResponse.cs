namespace MyMusic.Server.DTO.Songs;

public record PreviewSongDevicePathsResponse
{
    public required List<SongDevicePathPreviewItem> Items { get; init; }
}

/// <summary>
/// The path a song would be given on a device it is not on.
/// </summary>
public record SongDevicePathPreviewItem
{
    public required long SongId { get; init; }
    public required long DeviceId { get; init; }
    public required string Path { get; init; }
}
