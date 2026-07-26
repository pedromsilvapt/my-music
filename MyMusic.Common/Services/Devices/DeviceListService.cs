using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using MyMusic.Common.Filters;

namespace MyMusic.Common.Services.Devices;

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