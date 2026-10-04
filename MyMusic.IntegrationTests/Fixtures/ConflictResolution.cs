namespace MyMusic.IntegrationTests.Fixtures;

/// <summary>
/// What a sync does with a file changed differently on the device and on the server.
/// </summary>
public enum ConflictResolution
{
    Skip,
    Upload,
    Download,
}
