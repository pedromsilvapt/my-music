using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Devices;

public record CreateDeviceResponse
{
    public required CreateDeviceItem Device { get; init; }
}

public record CreateDeviceItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }

    public static CreateDeviceItem FromEntity(Entities.Device device) =>
        new()
        {
            Id = device.Id, Name = device.Name, Icon = device.Icon, Color = device.Color,
            NamingTemplate = device.NamingTemplate,
        };
}