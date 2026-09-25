using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.Songs;

public class SongHistoryQueryService(
    MusicDbContext db,
    ICurrentUser currentUser) : ISongHistoryQueryService
{
    /// <inheritdoc />
    public async Task<List<SongHistoryEntity>> GetSongHistoryAsync(long songId, CancellationToken cancellationToken = default)
    {
        // Ownership check: only expose history for songs that still exist and belong to the
        // current user. If the song has been deleted (no row in `songs`), we can no longer
        // verify ownership, so we return an empty list. The history rows are preserved in the
        // database but not exposed via the API after deletion.
        var owned = await db.Songs
            .AnyAsync(s => s.Id == songId && s.OwnerId == currentUser.Id, cancellationToken);

        if (!owned)
        {
            return [];
        }

        return await db.SongHistories
            .Where(h => h.SongId == songId)
            .OrderBy(h => h.SongRevision)
            .ToListAsync(cancellationToken);
    }
}