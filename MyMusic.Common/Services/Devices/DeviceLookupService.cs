using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

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