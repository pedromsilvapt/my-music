using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record SyncStartRequest
{
    public bool DryRun { get; init; }

    /// <summary>
    /// Direction of the sync. Stored on the session and used by every step. Defaults to
    /// <see cref="SyncDirection.Both"/>.
    /// </summary>
    public SyncDirection? Direction { get; init; }
    public string? RepositoryPath { get; init; }

    /// <summary>
    /// Match files that would be created on the server against the library (and the session's other
    /// uploads) by acoustic fingerprint, linking soundalikes instead of importing them.
    /// </summary>
    public bool Deduplicate { get; init; }

    public List<SyncScanErrorItem>? ScanErrors { get; init; }

    /// <summary>
    /// Device options to preview in a dry run without saving them to the device. Only allowed when
    /// <see cref="DryRun"/> is set.
    /// </summary>
    public SyncStartDeviceOptions? DeviceOptions { get; init; }
}

public record SyncStartDeviceOptions
{
    /// <summary>Naming template to use for the session; <c>null</c> means the server default.</summary>
    public string? NamingTemplate { get; init; }
}

public record SyncScanErrorItem
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
}

public record SyncStartResponse
{
    public required long SessionId { get; init; }
}