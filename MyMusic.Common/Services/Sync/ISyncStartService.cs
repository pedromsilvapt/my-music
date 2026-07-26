namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Input for a sync session start operation. Mirrors the server-side fields of
/// <c>SyncStartRequest</c> but lives in <see cref="MyMusic.Common"/> so the service has no
/// dependency on the Server DTO layer.
/// </summary>
public record SyncStartInput
{
    public bool DryRun { get; init; }

    public string? RepositoryPath { get; init; }

    public List<SyncStartScanError>? ScanErrors { get; init; }
}

/// <summary>
/// A single scan error reported by the client when starting a sync session.
/// </summary>
public record SyncStartScanError
{
    public required string FilePath { get; init; }

    public required string ErrorMessage { get; init; }
}

/// <summary>
/// Result of a sync session start operation. The controller maps this to
/// <c>SyncStartResponse</c>.
/// </summary>
public record SyncStartResult
{
    public required long SessionId { get; init; }
}

/// <summary>
/// Starts a new <see cref="DeviceSyncSession"/> for a device owned by the current user,
/// optionally recording client-reported scan errors. Extracted from
/// DevicesController.StartSync so the controller stays thin (input/output + DTO mapping only).
/// Reuses <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for the device identity check and
/// <see cref="ISyncActionsServerFactory"/> to record scan errors.
/// </summary>
public interface ISyncStartService
{
    /// <summary>
    /// Starts a sync session for <paramref name="deviceId"/> owned by <paramref name="ownerId"/>.
    /// Returns <c>null</c> when no such device exists for the owner (mirrors the previous
    /// controller <c>NotFound</c> path).
    /// </summary>
    Task<SyncStartResult?> StartAsync(
        long deviceId,
        long ownerId,
        SyncStartInput input,
        CancellationToken cancellationToken);
}