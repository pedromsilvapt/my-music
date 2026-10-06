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

    /// <summary>
    /// Copies of the songs to remove from their device, or to restore when waiting to be removed, one by one.
    /// </summary>
    public List<SongDeviceCopyItem>? Copies { get; init; }
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

    /// <summary>The copy the path is for. Required when the song is more than once on the device.</summary>
    public long? SongDeviceId { get; init; }
    public required string Path { get; init; }
}

public record SongDeviceCopyItem
{
    public required long SongDeviceId { get; init; }

    /// <summary>False removes the copy from its device, true restores a copy waiting to be removed.</summary>
    public required bool Include { get; init; }
}

public record UpdateSongDevicesResponse
{
    public required bool Success { get; init; }
}