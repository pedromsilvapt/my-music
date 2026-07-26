using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncDeviceSongsService"/>.
/// </summary>
public class SyncDeviceSongsService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ILogger<SyncDeviceSongsService> logger) : ISyncDeviceSongsService
{
    /// <inheritdoc />
    public async Task<SyncDeviceSongsResult?> GetAsync(
        long deviceId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var songs = await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => new SyncDeviceSongItem
            {
                SongId = sd.SongId,
                Path = sd.DevicePath,
                Action = sd.SyncAction != null ? sd.SyncAction.Value.ToString() : null,
            })
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} songs for device {DeviceId}", songs.Count, deviceId);

        return new SyncDeviceSongsResult { Songs = songs };
    }
}