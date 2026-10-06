namespace MyMusic.Server.DTO.Songs;

public record UpdateSongDevicesRequest
{
    public required List<long> SongIds { get; init; }
    public required List<SongDeviceUpdateItem> Updates { get; init; }

    /// <summary>
    /// Paths typed by the user for songs on devices: songs already on the device, or being added to it
    /// by <see cref="Updates"/>. The device applies each path as is on its next sync.
    /// </summary>
    public List<SongDevicePathItem>? Paths { get; init; }
}

public record SongDeviceUpdateItem
{
    public required long DeviceId { get; init; }
    public required bool Include { get; init; }
}

public record SongDevicePathItem
{
    public required long SongId { get; init; }
    public required long DeviceId { get; init; }
    public required string Path { get; init; }
}

public record UpdateSongDevicesResponse
{
    public required bool Success { get; init; }
}