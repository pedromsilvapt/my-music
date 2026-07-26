using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Deletes a <see cref="Device"/> and all of its associated data (sync sessions, sync session
/// records, SongDevices, and staging directories) for the current user. Reuses
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