using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Lists the most recent <see cref="DeviceSyncSession"/> entities for a device owned by the
/// current user, with their <see cref="DeviceSyncSession.Records"/> loaded for action-count
/// aggregation. Extracted from DevicesController.ListSessions. Reuses
/// <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for device identity checks so not-found / foreign-device
/// cases resolve to <c>null</c> (which the controller maps to <c>NotFound</c>).
/// </summary>
public interface ISyncSessionListService
{
    /// <summary>
    /// Lists up to <paramref name="count"/> most recent sessions for the device owned by
    /// <paramref name="ownerId"/>, ordered by <see cref="DeviceSyncSession.StartedAt"/>
    /// descending. Returns <c>null</c> when no such device exists for the owner.
    /// </summary>
    Task<SyncSessionListResult?> ListAsync(
        long deviceId,
        long ownerId,
        int count,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a sync session list operation.
/// </summary>
public record SyncSessionListResult
{
    public required List<DeviceSyncSession> Sessions { get; init; }
}