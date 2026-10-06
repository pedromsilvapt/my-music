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

        var songCounts = await db.SongDevices
            .Where(sd => sd.SongId != null && deviceIds.Contains(sd.DeviceId))
            .GroupBy(sd => sd.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count, cancellationToken);

        var entries = devices
            .Select(d => new DeviceListEntry { Device = d, SongCount = songCounts.GetValueOrDefault(d.Id) })
            .ToList();

        return new DeviceListResult { Devices = entries };
    }
}
