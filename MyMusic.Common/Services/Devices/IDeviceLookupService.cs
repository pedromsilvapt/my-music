using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Shared lookup helper for <see cref="Device"/> entities scoped to the current user.
/// </summary>
public interface IDeviceLookupService
{
    /// <summary>
    /// Finds a device owned by <paramref name="ownerId"/> by <paramref name="deviceId"/>.
    /// </summary>
    Task<Device?> FindDeviceAsync(MusicDbContext db, long deviceId, long ownerId, CancellationToken cancellationToken);
}