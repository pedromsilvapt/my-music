using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Deletes a <see cref="Device"/> and all of its associated data (sync sessions, sync session
/// records, SongDevices, and staging directories) for the current user. Extracted from
/// DevicesController.Delete so the controller stays thin (input/output only). Reuses
/// <see cref="IDeviceLookupService"/> for device identity and <see cref="StagingDirectoryCleanupService"/>
/// for staging cleanup to avoid duplicating that logic.
/// </summary>
public interface IDeviceDeleteService
{
    /// <summary>
    /// Deletes the device owned by the current user along with its sync sessions, sync session
    /// records, SongDevices, and any staging directories. Returns <c>false</c> when no such
    /// device exists (mirrors the previous controller <c>NotFound</c> path).
    /// </summary>
    Task<bool> DeleteAsync(long deviceId, CancellationToken cancellationToken);
}

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