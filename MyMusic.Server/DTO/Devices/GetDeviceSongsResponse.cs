namespace MyMusic.Server.DTO.Devices;

public record GetDeviceSongsResponse
{
    public required List<DeviceSongItem> Songs { get; init; }
}

public record DeviceSongItem
{
    public required long SongId { get; init; }
    public required string Path { get; init; }
    public string? Action { get; init; }
}