using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Services;

namespace MyMusic.Common.Services;

public class AlbumDeleteService(
    MusicDbContext db,
    ILogger<AlbumDeleteService> logger) : IAlbumDeleteService
{
    public async Task DeleteAsync(long[] albumIds, CancellationToken cancellationToken = default)
    {
        await db.AlbumSources
            .Where(a => albumIds.Contains(a.AlbumId))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted AlbumSources for {Count} albums", albumIds.Length);

        var count = await db.Albums
            .Where(a => albumIds.Contains(a.Id))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted {Count} Albums", count);
    }

    public async Task<long[]> DeleteIfUnusedAsync(long[] albumIds, CancellationToken cancellationToken = default)
    {
        if (albumIds.Length == 0) return [];

        var orphanedIds = await db.Albums
            .Where(a => albumIds.Contains(a.Id) && !a.Songs.Any())
            .Select(a => a.Id)
            .ToArrayAsync(cancellationToken);

        if (orphanedIds.Length == 0) return [];

        await DeleteAsync(orphanedIds, cancellationToken);
        return orphanedIds;
    }
}
