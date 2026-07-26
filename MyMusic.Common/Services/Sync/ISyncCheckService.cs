using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// A single client-reported file descriptor for a sync check request. Mirrors the server-side
/// fields of <c>SyncFileInfoItem</c> but lives in <see cref="MyMusic.Common"/> so the service
/// has no dependency on the Server DTO layer.
/// </summary>
public record SyncCheckFileInfo
{
    public required string Path { get; init; }

    public required DateTime ModifiedAt { get; init; }

    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Input for a sync check operation. Mirrors <c>SyncCheckRequest</c> without the DTO dependency.
/// </summary>
public record SyncCheckInput
{
    public required List<SyncCheckFileInfo> Files { get; init; }

    public bool Force { get; init; }
}

/// <summary>
/// Result of a sync check operation. The controller maps this to <c>SyncCheckResponse</c>.
/// </summary>
public record SyncCheckResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
}

/// <summary>
/// Compares client-reported device files against the server's <see cref="SongDevice"/> state for
/// a device owned by the current user, producing <see cref="DeviceSyncSessionRecord"/> entries
/// describing the required sync actions (create/update/delete remote, update local, conflict,
/// skipped, ...). Reuses <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> and <see cref="ISyncSessionLookupService"/>
/// for identity checks, <see cref="ISyncPathResolver"/> for naming/path-collision logic,
/// <see cref="ISyncComparisonHelper"/> for timestamp comparisons, and
/// <see cref="ISyncActionsServerFactory"/> to persist the records that are not tentative.
/// </summary>
public interface ISyncCheckService
{
    /// <summary>
    /// Runs the sync check for <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Returns <c>null</c> when no such device or session
    /// exists for the owner (mirrors the previous controller <c>NotFound</c> path). Throws when
    /// the session is not in progress (mirrors the previous controller behavior).
    /// </summary>
    Task<SyncCheckResult?> CheckAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncCheckInput input,
        CancellationToken cancellationToken);
}