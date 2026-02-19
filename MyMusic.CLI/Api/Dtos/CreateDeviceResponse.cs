namespace MyMusic.CLI.Api.Dtos;

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
}