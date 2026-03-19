using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Devices;

public record GetDeviceResponse
{
    public required GetDeviceItem Device { get; init; }
}

public record GetDeviceItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
    public required int SongCount { get; init; }
    public bool ImportOnPurchase { get; init; }

    public static GetDeviceItem FromEntity(Entities.Device device, int songCount) =>
        new()
        {
            Id = device.Id,
            Name = device.Name,
            Icon = device.Icon,
            Color = device.Color,
            NamingTemplate = device.NamingTemplate,
            SongCount = songCount,
            ImportOnPurchase = device.ImportOnPurchase,
        };
}
