using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

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
/// controller stays thin (input/output + DTO mapping only). Reuses <see cref="IDeviceLookupService"/>
/// for the device identity check.
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