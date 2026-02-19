namespace MyMusic.Server.DTO.Songs;

public record GetSongDevicesResponse
{
    public required List<SongDeviceItem> Devices { get; init; }
}

public record SongDeviceItem
{
    public required long DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public string? DeviceIcon { get; init; }
    public string? DeviceColor { get; init; }
    public string? Path { get; init; }
    public string? SyncAction { get; init; }
}