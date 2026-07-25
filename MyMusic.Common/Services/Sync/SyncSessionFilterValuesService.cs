using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Returns the distinct values for a single sync-session record filter field, scoped to the
/// current owner and (for file paths) the target session/device. Extracted from
/// <see cref="MyMusic.Server.Controllers.DevicesController"/> so the controller stays thin
/// (input/output + DTO mapping only). The filter metadata endpoint (pure static output) remains
/// in the controller.
/// </summary>
public interface ISyncSessionFilterValuesService
{
    /// <summary>
    /// Returns the distinct, ordered, optionally searched, and limited values for the given
    /// <paramref name="field"/> for the session identified by <paramref name="sessionId"/> on the
    /// device <paramref name="deviceId"/> owned by <paramref name="ownerId"/>. Unknown fields
    /// return an empty list (mirrors the prior controller behavior).
    /// </summary>
    Task<SyncSessionFilterValuesResult> GetAsync(
        long ownerId,
        long deviceId,
        long sessionId,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a sync session filter-values operation. The controller maps this to
/// <c>FilterValuesResponse</c>.
/// </summary>
public record SyncSessionFilterValuesResult
{
    public required List<string> Values { get; init; }
}

/// <summary>
/// Default implementation of <see cref="ISyncSessionFilterValuesService"/>.
/// </summary>
public class SyncSessionFilterValuesService(MusicDbContext db) : ISyncSessionFilterValuesService
{
    /// <inheritdoc />
    public async Task<SyncSessionFilterValuesResult> GetAsync(
        long ownerId,
        long deviceId,
        long sessionId,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<string>? query = field.ToLower() switch
        {
            "filepath" => db.DeviceSyncSessionRecords
                .Where(r => r.SessionId == sessionId && r.Session.DeviceId == deviceId && r.Session.Device.OwnerId == ownerId)
                .Select(r => r.FilePath)
                .Distinct(),
            "song.title" => db.Songs
                .Where(s => s.OwnerId == ownerId)
                .Select(s => s.Title)
                .Distinct(),
            "song.artist.name" => db.Artists
                .Where(a => a.OwnerId == ownerId)
                .Select(a => a.Name)
                .Distinct(),
            "song.album.name" => db.Albums
                .Where(a => a.OwnerId == ownerId)
                .Select(a => a.Name)
                .Distinct(),
            _ => null,
        };

        if (query == null)
        {
            return new SyncSessionFilterValuesResult { Values = [] };
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v != null && v.ToLower().Contains(searchLower));
        }

        var values = await query
            .Where(v => v != null)
            .OrderBy(v => v)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new SyncSessionFilterValuesResult { Values = values! };
    }
}