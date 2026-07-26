namespace MyMusic.Common.Services.Sync;

/// <summary>
/// A single song-device mapping projected for the device songs response.
/// </summary>
public record SyncDeviceSongItem
{
    public required long? SongId { get; init; }

    public required string Path { get; init; }

    public string? Action { get; init; }
}

/// <summary>
/// Result of a device-songs query. The controller maps this to <c>GetDeviceSongsResponse</c>.
/// </summary>
public record SyncDeviceSongsResult
{
    public required List<SyncDeviceSongItem> Songs { get; init; }
}

/// <summary>
/// Lists the <see cref="SongDevice"/> entries for a device owned by the current user, projecting
/// <c>SongId</c>, <c>DevicePath</c> and the current <see cref="SongSyncAction"/> (if any). Extracted
/// from DevicesController.GetDeviceSongs as part of Phase 10 of the controllers refactor so the
/// controller stays thin (input/output + DTO mapping only). Reuses
/// <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for the device identity check.
/// </summary>
public interface ISyncDeviceSongsService
{
    /// <summary>
    /// Returns the songs for <paramref name="deviceId"/> owned by <paramref name="ownerId"/>.
    /// Returns <c>null</c> when no such device exists for the owner (mirrors the previous controller
    /// <c>NotFound</c> path).
    /// </summary>
    Task<SyncDeviceSongsResult?> GetAsync(
        long deviceId,
        long ownerId,
        CancellationToken cancellationToken);
}