using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using MyMusic.Common.Filters;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Lists the current user's <see cref="Device"/> entities with optional fuzzy search,
/// DSL filtering, and per-device song counts/refs. Extracted from DevicesController.List.
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

/// <summary>
/// Default implementation of <see cref="IDeviceListService"/>.
/// </summary>
public class DeviceListService(MusicDbContext db) : IDeviceListService
{
    /// <inheritdoc />
    public async Task<DeviceListResult> ListAsync(
        long ownerId,
        string? search,
        string? filter,
        bool includeSongs,
        CancellationToken cancellationToken)
    {
        var query = db.Devices
            .Where(d => d.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, d => d.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Device>(filter);
            query = query.Where(filterExpression);
        }

        var devices = await query.ToListAsync(cancellationToken);
        var deviceIds = devices.Select(d => d.Id).ToList();

        var songDeviceGroups = await db.SongDevices
            .Where(sd => sd.SongId != null && deviceIds.Contains(sd.DeviceId))
            .GroupBy(sd => sd.DeviceId)
            .Select(g => new
            {
                DeviceId = g.Key,
                Count = g.Count(),
                SongRefs = includeSongs ? g.ToList() : null,
            })
            .ToDictionaryAsync(x => x.DeviceId, x => x, cancellationToken);

        var entries = devices.Select(d =>
        {
            var group = songDeviceGroups.GetValueOrDefault(d.Id);
            var songRefs = includeSongs
                ? group?.SongRefs?.Select(sd => new DeviceListSongRef
                {
                    SongId = sd.SongId!.Value,
                    DevicePath = sd.DevicePath,
                    SyncAction = sd.SyncAction,
                }).ToList()
                : null;
            return new DeviceListEntry
            {
                Device = d,
                SongCount = group?.Count ?? 0,
                SongRefs = songRefs,
            };
        }).ToList();

        return new DeviceListResult { Devices = entries };
    }
}