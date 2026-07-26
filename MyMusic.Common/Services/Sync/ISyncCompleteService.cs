namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Result of a sync session complete operation. The controller maps this to
/// <c>SyncCompleteResponse</c>.
/// </summary>
public record SyncCompleteResult
{
    public required int CreateRemoteCount { get; init; }

    public required int UpdateRemoteCount { get; init; }

    public required int SkippedCount { get; init; }

    public required int CreateLocalCount { get; init; }

    public required int UpdateLocalCount { get; init; }

    public required int DeleteLocalCount { get; init; }

    public required int LinkCount { get; init; }

    public required int UnlinkCount { get; init; }

    public required int RenameCount { get; init; }

    public required int ConflictCount { get; init; }

    public required int UpdateTimestampCount { get; init; }

    public required int ErrorCount { get; init; }
}

/// <summary>
/// Failure reasons returned by <see cref="ISyncCompleteService.CompleteAsync"/>. The controller
/// maps each value to the appropriate HTTP response (NotFound / thrown exception) without
/// leaking exceptions.
/// </summary>
public enum SyncCompleteFailure
{
    NotFound,
}

/// <summary>
/// Completes a <see cref="DeviceSyncSession"/> previously committed for a device owned by the
/// current user, updating the device's <see cref="Device.LastSyncAt"/> (non-dry-run only) and
/// aggregating the session's records into per-action counts. Extracted from
/// DevicesController.CompleteSync so the controller stays thin. Reuses
/// <see cref="ISyncSessionLookupService"/> for the session identity check.
/// </summary>
public interface ISyncCompleteService
{
    /// <summary>
    /// Completes the session <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Throws when the session is still in progress or is
    /// not in the <see cref="SyncSessionStatus.Committed"/> state, mirroring the previous
    /// controller behavior. Returns <c>null</c> when no such session exists (NotFound).
    /// </summary>
    Task<SyncCompleteResult?> CompleteAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken);
}