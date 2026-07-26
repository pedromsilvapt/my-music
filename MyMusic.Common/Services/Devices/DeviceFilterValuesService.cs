using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceFilterValuesService"/>.
/// </summary>
public class DeviceFilterValuesService(MusicDbContext db) : IDeviceFilterValuesService
{
    /// <inheritdoc />
    public async Task<DeviceFilterValuesResult> GetAsync(
        long ownerId,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<string>? query = field switch
        {
            "name" => db.Devices
                .Where(d => d.OwnerId == ownerId)
                .Select(d => d.Name)
                .Distinct(),
            "icon" => db.Devices
                .Where(d => d.OwnerId == ownerId)
                .Select(d => d.Icon)
                .Where(v => v != null)
                .Cast<string>()
                .Distinct(),
            "color" => db.Devices
                .Where(d => d.OwnerId == ownerId)
                .Select(d => d.Color)
                .Where(v => v != null)
                .Cast<string>()
                .Distinct(),
            _ => null,
        };

        if (query == null)
        {
            return new DeviceFilterValuesResult { Values = [] };
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v.ToLower().Contains(searchLower));
        }

        var values = await query
            .OrderBy(v => v)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new DeviceFilterValuesResult { Values = values };
    }
}