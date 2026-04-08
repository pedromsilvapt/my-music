using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Devices;

public record ListDevicesResponse
{
    public required List<ListDeviceItem> Devices { get; init; }
}

public record DeviceSongRef
{
    public required long Id { get; init; }
    public required string Path { get; init; }
}

public record ListDeviceItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
    public required int SongCount { get; init; }
    public required List<DeviceSongRef> Songs { get; init; }
    public bool ImportOnPurchase { get; init; }

    public static ListDeviceItem FromEntity(Entities.Device device, int songCount, List<DeviceSongRef> songs) =>
        new()
        {
            Id = device.Id, Name = device.Name, Icon = device.Icon, Color = device.Color,
            NamingTemplate = device.NamingTemplate, SongCount = songCount, Songs = songs,
            ImportOnPurchase = device.ImportOnPurchase,
        };
}