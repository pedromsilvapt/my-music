namespace MyMusic.IntegrationTests.Models;

public record SongDeviceValidation
{
    public required string SongTitle { get; init; }
    public string? ExpectedPath { get; init; }
    public string? ExpectedSyncAction { get; init; }
    public bool ShouldExist { get; init; } = true;
}
