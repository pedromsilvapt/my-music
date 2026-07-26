using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceGetService"/>.
/// </summary>
public class DeviceGetService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup) : IDeviceGetService
{
    /// <inheritdoc />
    public async Task<DeviceGetResult?> GetAsync(long deviceId, long ownerId, CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var songCount = await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .CountAsync(cancellationToken);

        return new DeviceGetResult { Device = device, SongCount = songCount };
    }
}