using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceDeleteService"/>.
/// </summary>
public class DeviceDeleteService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ICurrentUser currentUser,
    IFileSystem fileSystem,
    ILogger<DeviceDeleteService> logger) : IDeviceDeleteService
{
    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long deviceId, CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, currentUser.Id, cancellationToken);
        if (device == null) return false;

        var sessionsForDevice = await db.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .Select(s => new { s.Id, s.RepositoryPath })
            .ToListAsync(cancellationToken);

        foreach (var session in sessionsForDevice)
        {
            StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);
        }

        await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.DeviceSyncSessionRecords
            .Where(r => db.DeviceSyncSessions
                .Where(s => s.DeviceId == deviceId)
                .Select(s => s.Id)
                .Contains(r.SessionId))
            .ExecuteDeleteAsync(cancellationToken);

        await db.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Deleted device {DeviceId} for user {UserId}", deviceId, currentUser.Id);

        return true;
    }
}