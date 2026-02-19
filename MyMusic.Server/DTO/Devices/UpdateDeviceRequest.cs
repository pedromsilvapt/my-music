namespace MyMusic.Server.DTO.Devices;

public record UpdateDeviceRequest
{
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
}

public record UpdateDeviceResponse
{
    public required UpdateDeviceItem Device { get; init; }
}

public record UpdateDeviceItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
}