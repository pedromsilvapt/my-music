using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Lists the current user's <see cref="Device"/> entities with optional fuzzy search,
/// DSL filtering, and per-device song counts.
/// </summary>
public interface IDeviceListService
{
    /// <summary>
    /// Lists devices owned by <paramref name="ownerId"/>, applying the optional search and
    /// filter expressions, and computing per-device song counts.
    /// </summary>
    Task<DeviceListResult> ListAsync(
        long ownerId,
        string? search,
        string? filter,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a device list operation.
/// </summary>
public record DeviceListResult
{
    public required List<DeviceListEntry> Devices { get; init; }
}

/// <summary>
/// A single device row in a list result, paired with its computed song count.
/// </summary>
public record DeviceListEntry
{
    public required Device Device { get; init; }
    public required int SongCount { get; init; }
}
