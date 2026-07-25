using Microsoft.EntityFrameworkCore;

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

/// <summary>
/// Default implementation of <see cref="IDeviceLookupService"/>.
/// </summary>
public class DeviceLookupService : IDeviceLookupService
{
    /// <inheritdoc />
    public async Task<Device?> FindDeviceAsync(MusicDbContext db, long deviceId, long ownerId, CancellationToken cancellationToken)
    {
        return await db.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == ownerId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}