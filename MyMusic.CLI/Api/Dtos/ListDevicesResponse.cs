namespace MyMusic.CLI.Api.Dtos;

public record ListDevicesResponse
{
    public required List<ListDeviceItem> Devices { get; init; }
}

public record DeviceSongRef
{
    public required long Id { get; init; }
    public required string Path { get; init; }
    public string? SyncAction { get; init; }
}

public record ListDeviceItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
    public required int SongCount { get; init; }
    public List<DeviceSongRef>? Songs { get; init; }
    public bool ImportOnPurchase { get; init; }
    public DateTime? LastSyncAt { get; init; }
}