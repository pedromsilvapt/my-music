using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncSessionGetService"/>.
/// </summary>
public class SyncSessionGetService(MusicDbContext db) : ISyncSessionGetService
{
    /// <inheritdoc />
    public async Task<DeviceSyncSession?> GetAsync(
        long sessionId,
        long deviceId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        return await db.DeviceSyncSessions
            .Include(s => s.Records)
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == ownerId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
