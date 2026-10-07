namespace MyMusic.IntegrationTests.Fixtures;

public record SyncOptions
{
    public bool Force { get; init; }
    public bool AutoConfirm { get; init; } = true;
    public bool DryRun { get; init; }
    public bool Deduplicate { get; init; }
    /// <summary>
    /// Keeps the Skipped records after the sync, so tests can assert on them. On by default (unlike the
    /// applications); only a test about the default behavior turns it off.
    /// </summary>
    public bool RecordSkipped { get; init; } = true;
    public SyncDirection? Direction { get; init; }
    /// <summary>How real conflicts are resolved. When null, they are left unresolved.</summary>
    public ConflictResolution? Conflicts { get; init; }
}
