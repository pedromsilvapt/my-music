using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Returns the distinct values for a single device filter field, scoped to the current
/// owner. Extracted from DevicesController.GetFilterValues so the controller stays thin
/// (input/output + DTO mapping only). The filter metadata endpoint (pure static output)
/// remains in the controller.
/// </summary>
public interface IDeviceFilterValuesService
{
    /// <summary>
    /// Returns the distinct, ordered, optionally searched, and limited values for the
    /// given <paramref name="field"/> across the devices owned by <paramref name="ownerId"/>.
    /// Unknown fields return an empty list (mirrors the prior controller behavior).
    /// </summary>
    Task<DeviceFilterValuesResult> GetAsync(
        long ownerId,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a device filter-values operation. The controller maps this to
/// <c>FilterValuesResponse</c>.
/// </summary>
public record DeviceFilterValuesResult
{
    public required List<string> Values { get; init; }
}

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