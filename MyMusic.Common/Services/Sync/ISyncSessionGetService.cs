using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Retrieves a single <see cref="DeviceSyncSession"/> for a device owned by the current user, with its
/// <see cref="DeviceSyncSession.Records"/> loaded for action-count aggregation.
/// </summary>
public interface ISyncSessionGetService
{
    /// <summary>
    /// Returns the session <paramref name="sessionId"/> of device <paramref name="deviceId"/> owned by
    /// <paramref name="ownerId"/>, or <c>null</c> when no such session exists for the owner.
    /// </summary>
    Task<DeviceSyncSession?> GetAsync(
        long sessionId,
        long deviceId,
        long ownerId,
        CancellationToken cancellationToken);
}
