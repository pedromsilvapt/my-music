using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Result of a create-pending-actions operation. The controller maps this to
/// <c>CreatePendingActionsResponse</c>.
/// </summary>
public record SyncPendingActionsResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
}

/// <summary>
/// Generates <see cref="DeviceSyncSessionRecord"/> entries for a device's pending <see cref="SongDevice"/>
/// sync actions (<see cref="SongSyncAction.Remove"/>, <see cref="SongSyncAction.Download"/>), applying
/// naming-template path resolution and unique-path collision handling via <see cref="ISyncPathResolver"/>.
/// Extracted from DevicesController.CreatePendingActions (and its private
/// <c>CreatePendingActionsForDevice</c> helper) as part of Phase 10 of the controllers refactor so the
/// controller stays thin (input/output + DTO mapping only). Reuses
/// <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for the device identity check and
/// <see cref="ISyncPathResolver"/> for naming/path-collision logic.
/// </summary>
public interface ISyncPendingActionsService
{
    /// <summary>
    /// Creates pending-action records for <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Returns <c>null</c> when no such device exists for the owner
    /// (mirrors the previous controller <c>NotFound</c> path).
    /// </summary>
    Task<SyncPendingActionsResult?> CreateAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken);
}