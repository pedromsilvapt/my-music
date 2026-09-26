using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Services.SongHistory;

namespace MyMusic.Common.Services.Songs;

public class SongHistoryPendingService(MusicDbContext db) : ISongHistoryPendingService
{
    /// <inheritdoc />
    public Task<bool> HasPendingAsync(long songId, CancellationToken cancellationToken = default) =>
        db.SongHistoryQueues.AnyAsync(
            q => q.SongId == songId
                && q.ProcessedAt == null
                && q.ErrorCount < SongHistoryWorker.MaxErrorCount,
            cancellationToken);
}
