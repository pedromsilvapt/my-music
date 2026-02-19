namespace MyMusic.Server.DTO.Songs;

public record UpdateSongDevicesRequest
{
    public required List<long> SongIds { get; init; }
    public required List<SongDeviceUpdateItem> Updates { get; init; }
}

public record SongDeviceUpdateItem
{
    public required long DeviceId { get; init; }
    public required bool Include { get; init; }
}

public record UpdateSongDevicesResponse
{
    public required bool Success { get; init; }
}