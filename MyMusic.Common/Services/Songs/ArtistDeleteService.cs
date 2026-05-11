using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Services;

namespace MyMusic.Common.Services;

public class ArtistDeleteService(
    MusicDbContext db,
    ILogger<ArtistDeleteService> logger) : IArtistDeleteService
{
    public async Task DeleteAsync(long[] artistIds, CancellationToken cancellationToken = default)
    {
        await db.ArtistSources
            .Where(a => artistIds.Contains(a.ArtistId))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted ArtistSources for {Count} artists", artistIds.Length);

        var count = await db.Artists
            .Where(a => artistIds.Contains(a.Id))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted {Count} Artists", count);
    }

    public async Task<long[]> DeleteIfUnusedAsync(long[] artistIds, CancellationToken cancellationToken = default)
    {
        if (artistIds.Length == 0) return [];

        var orphanedIds = await db.Artists
            .Where(a => artistIds.Contains(a.Id) && !a.Songs.Any() && !a.Albums.Any())
            .Select(a => a.Id)
            .ToArrayAsync(cancellationToken);

        if (orphanedIds.Length == 0) return [];

        await DeleteAsync(orphanedIds, cancellationToken);
        return orphanedIds;
    }
}
