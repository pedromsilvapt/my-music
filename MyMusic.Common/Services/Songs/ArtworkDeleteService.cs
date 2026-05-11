using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Services;

namespace MyMusic.Common.Services;

public class ArtworkDeleteService(
    MusicDbContext db,
    ILogger<ArtworkDeleteService> logger) : IArtworkDeleteService
{
    public async Task DeleteAsync(long[] artworkIds, CancellationToken cancellationToken = default)
    {
        if (artworkIds.Length == 0) return;

        var count = await db.Artworks
            .Where(a => artworkIds.Contains(a.Id))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted {Count} Artworks", count);
    }

    public async Task<long[]> DeleteIfUnusedAsync(long[] artworkIds, CancellationToken cancellationToken = default)
    {
        if (artworkIds.Length == 0) return [];

        var unusedIds = await db.Artworks
            .Where(a => artworkIds.Contains(a.Id)
                && !db.Songs.Any(s => s.CoverId == a.Id)
                && !db.Albums.Any(al => al.CoverId == a.Id)
                && !db.Artists.Any(ar => ar.PhotoId == a.Id || ar.BackgroundId == a.Id))
            .Select(a => a.Id)
            .ToArrayAsync(cancellationToken);

        if (unusedIds.Length == 0) return [];

        await DeleteAsync(unusedIds, cancellationToken);
        return unusedIds;
    }
}
