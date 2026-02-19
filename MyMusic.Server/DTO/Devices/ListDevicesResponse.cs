using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Devices;

public record ListDevicesResponse
{
    public required List<ListDeviceItem> Devices { get; init; }
}

public record ListDeviceItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
    public required int SongCount { get; init; }

    public static ListDeviceItem FromEntity(Entities.Device device, int songCount) =>
        new()
        {
            Id = device.Id, Name = device.Name, Icon = device.Icon, Color = device.Color,
            NamingTemplate = device.NamingTemplate, SongCount = songCount,
        };
}