using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Services.BackgroundJobs;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Reports the legacy play count revision cleanup run by <see cref="SongHistoryWorker"/> once the baseline backfill is
/// complete. Queued are the user's revisions that hold nothing but a play count change. Removed revisions leave no
/// record and failures are only logged, so neither is counted.
/// </summary>
public class SongHistoryPlayCountCleanupJob : IQueuedBackgroundJob
{
    public string Key => "song-history-play-count-cleanup";

    public async Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId,
        CancellationToken cancellationToken)
    {
        var queued = await SongHistoryPlayCountCleanupService.PlayCountOnlyRevisions(db)
            .Where(h => db.Songs.Any(s => s.Id == h.SongId && s.OwnerId == userId))
            .CountAsync(cancellationToken);

        return new BackgroundJobCounters(queued, null, null);
    }

    public Task<BackgroundJobFailurePage> GetFailuresAsync(MusicDbContext db, long userId, int page, int pageSize,
        CancellationToken cancellationToken) => Task.FromResult(BackgroundJobFailurePage.Empty);
}
