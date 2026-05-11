using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Services;

namespace MyMusic.Common.Services;

public class GenreDeleteService(
    MusicDbContext db,
    ILogger<GenreDeleteService> logger) : IGenreDeleteService
{
    public async Task DeleteAsync(long[] genreIds, CancellationToken cancellationToken = default)
    {
        var count = await db.Genres
            .Where(g => genreIds.Contains(g.Id))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted {Count} Genres", count);
    }

    public async Task<long[]> DeleteIfUnusedAsync(long[] genreIds, CancellationToken cancellationToken = default)
    {
        if (genreIds.Length == 0) return [];

        var orphanedIds = await db.Genres
            .Where(g => genreIds.Contains(g.Id) && !g.Songs.Any())
            .Select(g => g.Id)
            .ToArrayAsync(cancellationToken);

        if (orphanedIds.Length == 0) return [];

        await DeleteAsync(orphanedIds, cancellationToken);
        return orphanedIds;
    }
}
