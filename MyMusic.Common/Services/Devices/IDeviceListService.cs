using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Lists the current user's <see cref="Device"/> entities with optional fuzzy search,
/// DSL filtering, and per-device song counts/refs.
/// </summary>
public interface IDeviceListService
{
    /// <summary>
    /// Lists devices owned by <paramref name="ownerId"/>, applying the optional search and
    /// filter expressions, and computing per-device song counts (and optional song refs).
    /// </summary>
    Task<DeviceListResult> ListAsync(
        long ownerId,
        string? search,
        string? filter,
        bool includeSongs,
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
/// A single device row in a list result, paired with its computed song count and
/// (optionally) the song references that produced it.
/// </summary>
public record DeviceListEntry
{
    public required Device Device { get; init; }
    public required int SongCount { get; init; }
    public List<DeviceListSongRef>? SongRefs { get; init; }
}

/// <summary>
/// A reference to a song synced onto a device, used by <see cref="DeviceListEntry.SongRefs"/>.
/// </summary>
public record DeviceListSongRef
{
    public required long SongId { get; init; }
    public required string DevicePath { get; init; }
    public SongSyncAction? SyncAction { get; init; }
}