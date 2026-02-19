namespace MyMusic.CLI.Api.Dtos;

public record CreateDeviceRequest
{
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
}