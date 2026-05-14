namespace MyMusic.IntegrationTests.Fixtures;

public record SyncOptions
{
    public bool Force { get; init; }
    public bool AutoConfirm { get; init; } = true;
    public bool DryRun { get; init; }
    public SyncDirection? Direction { get; init; }
}
