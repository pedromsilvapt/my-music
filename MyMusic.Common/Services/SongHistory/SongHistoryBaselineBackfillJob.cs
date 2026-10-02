using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Services.BackgroundJobs;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Reports the <c>created</c> baseline backfill run by <see cref="SongHistoryWorker"/>. Queued are the user's songs
/// still missing their baseline. Recorded baselines are indistinguishable from those recorded through the queue, and
/// failures are only logged, so neither is counted.
/// </summary>
public class SongHistoryBaselineBackfillJob : IQueuedBackgroundJob
{
    public string Key => "song-history-baseline";

    /// <summary>
    /// Songs with unprocessed queue entries are left out: they are already counted by the <c>song-history</c> job, and
    /// a newly created song gets its baseline from the queue anyway.
    /// </summary>
    public async Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId,
        CancellationToken cancellationToken)
    {
        var queued = await SongHistoryBaselineBackfillService.SongsMissingBaseline(db)
            .Where(s => s.OwnerId == userId)
            .Where(s => !db.SongHistoryQueues.Any(q => q.SongId == s.Id && q.ProcessedAt == null))
            .CountAsync(cancellationToken);

        return new BackgroundJobCounters(queued, null, null);
    }

    public Task<BackgroundJobFailurePage> GetFailuresAsync(MusicDbContext db, long userId, int page, int pageSize,
        CancellationToken cancellationToken) => Task.FromResult(BackgroundJobFailurePage.Empty);
}
