using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// A single client-reported conflict descriptor for a sync resolve-conflicts request. Mirrors the
/// server-side fields of <c>SyncConflictResolveItem</c> but lives in <see cref="MyMusic.Common"/>
/// so the service has no dependency on the Server DTO layer.
/// </summary>
public record SyncResolveConflictItem
{
    public required string Path { get; init; }

    public required long SongId { get; init; }

    public required string FileContentBase64 { get; init; }

    public required DateTime LocalModifiedAt { get; init; }
}

/// <summary>
/// A single client-reported potential-update descriptor for a sync resolve-conflicts request.
/// Mirrors <c>SyncPotentialUpdateResolveItem</c> without the DTO dependency.
/// </summary>
public record SyncResolvePotentialUpdateItem
{
    public required string Path { get; init; }

    public required long SongId { get; init; }

    public required string FileContentBase64 { get; init; }

    public required DateTime LocalModifiedAt { get; init; }

    public required DateTime LastSyncedAt { get; init; }
}

/// <summary>
/// Input for a sync resolve-conflicts operation. Mirrors <c>SyncResolveConflictsRequest</c>
/// without the DTO dependency.
/// </summary>
public record SyncResolveConflictsInput
{
    public required List<SyncResolveConflictItem> Conflicts { get; init; }

    public required List<SyncResolvePotentialUpdateItem> PotentialUpdates { get; init; }
}

/// <summary>
/// Result of a sync resolve-conflicts operation. The controller maps this to
/// <c>SyncResolveConflictsResponse</c>.
/// </summary>
public record SyncResolveConflictsResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
}

/// <summary>
/// Resolves client-reported sync conflicts and potential updates for a device sync session owned
/// by the current user. For each conflict/potential-update, the local file content (base64) is
/// compared against the server song checksum: matching checksums produce a timestamp-update record
/// (no file transfer), while differing checksums produce a conflict record (conflicts) or an
/// update-local record (potential updates, optionally followed by a rename record when the naming
/// template changed the target path). Extracted from <c>DevicesController.ResolveConflicts</c>
/// (the ~190-line method) as part of Phase 12 of the controllers refactor so the controller stays
/// thin (input/output + DTO mapping only). Reuses <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> and
/// <see cref="ISyncSessionLookupService"/> for identity checks, <see cref="ISyncPathResolver"/>
/// for naming/path-collision logic, and <see cref="ISyncActionsServerFactory"/> to persist the
/// records.
/// </summary>
public interface ISyncResolveConflictsService
{
    /// <summary>
    /// Resolves the conflicts/potential-updates for <paramref name="sessionId"/> scoped to
    /// <paramref name="deviceId"/> owned by <paramref name="ownerId"/>. Returns <c>null</c> when
    /// no such device or session exists for the owner (mirrors the previous controller
    /// <c>NotFound</c> path). Throws when the session is not in progress (mirrors the previous
    /// controller behavior).
    /// </summary>
    Task<SyncResolveConflictsResult?> ResolveAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncResolveConflictsInput input,
        CancellationToken cancellationToken);
}