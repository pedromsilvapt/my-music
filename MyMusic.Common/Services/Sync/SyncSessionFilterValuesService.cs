using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Services.Sync;

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