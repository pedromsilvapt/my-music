using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncSessionListService"/>.
/// </summary>
public class SyncSessionListService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup) : ISyncSessionListService
{
    /// <inheritdoc />
    public async Task<SyncSessionListResult?> ListAsync(
        long deviceId,
        long ownerId,
        int count,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var sessions = await db.DeviceSyncSessions
            .Include(s => s.Records)
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return new SyncSessionListResult { Sessions = sessions };
    }
}