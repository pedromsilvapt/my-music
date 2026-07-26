using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Fetches a single <see cref="Device"/> owned by a user along with its song count.
/// Extracted from DevicesController.Get. Reuses <see cref="IDeviceLookupService"/> for
/// the device lookup to keep device identity operations centralized.
/// </summary>
public interface IDeviceGetService
{
    /// <summary>
    /// Gets a device owned by <paramref name="ownerId"/> with its current song count, or
    /// <c>null</c> when no such device exists.
    /// </summary>
    Task<DeviceGetResult?> GetAsync(long deviceId, long ownerId, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a device get operation.
/// </summary>
public record DeviceGetResult
{
    public required Device Device { get; init; }
    public required int SongCount { get; init; }
}