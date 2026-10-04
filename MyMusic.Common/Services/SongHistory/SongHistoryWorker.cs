using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.BackgroundJobs;
using MyMusic.Common.Services.SongHistory.Models;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Polling background service that drains the <c>song_history_queues</c> table.
/// Cycles run back to back while there is work: the worker only waits
/// <c>SongHistoryWorkerIntervalSeconds</c> after a cycle that found no song to
/// process. Each queue row (written by a DB trigger on song/album/artist/genre/cover
/// mutations) contains the full JSON snapshot of the song at the moment of the
/// change (captured by BEFORE triggers, i.e. the pre-action state). The worker
/// batches queue rows by distinct <c>SongId</c>, fetches the live current
/// state of each song via <see cref="ISongHistorySnapshotService"/>, groups the
/// pending rows for that song by <c>TransactionId</c> (null TransactionId → each
/// row is its own group), selects the lowest-revision row per group as the
/// pre-transaction snapshot, and walks newest → oldest computing deltas against
/// the next-newer state (or the live state for the newest group). Thumbnail
/// replacement is applied to the delta's cover <c>FieldChange</c> before
/// inserting the <c>song_history</c> row.
/// <para>
/// A song that fails is not retried until the worker interval has passed since
/// the failure (<c>LastErrorAt</c>), so its attempts stay one interval apart
/// even though cycles do not wait. Rows that fail <see cref="MaxErrorCount"/> times
/// (default 3) are dead-lettered: left in the queue with <c>ErrorCount &gt;= 3</c>
/// and never retried. The <c>songs</c> BEFORE DELETE trigger blocks deletion
/// of a song that still has dead-lettered queue entries.
/// </para>
/// <para>
/// Each cycle then records the <c>created</c> baseline of songs still missing one and, once that is complete, removes
/// the legacy revisions holding nothing but a play count change. Their progress is reported by
/// <see cref="SongHistoryBaselineBackfillJob"/> and <see cref="SongHistoryPlayCountCleanupJob"/>.
/// </para>
/// </summary>
public class SongHistoryWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<Config> config,
    ISongHistoryNotifier notifier,
    ILogger<SongHistoryWorker> logger) : BackgroundService, IQueuedBackgroundJob
{
    /// <summary>
    /// Maximum number of processing attempts before a queue entry is dead-lettered.
    /// </summary>
    public const int MaxErrorCount = 3;

    public string Key => "song-history";

    private static readonly ActivitySource ActivitySource = new("MyMusic.SongHistoryWorker");

    /// <summary>
    /// Set once no song is missing its <c>created</c> baseline. Songs created from then on get theirs through the
    /// queue, so the backfill does not need to scan the songs again until the next restart.
    /// </summary>
    private bool _baselineBackfillComplete;

    /// <summary>
    /// Set once every song's history was cleared of its play count revisions; new ones are never recorded.
    /// </summary>
    private bool _playCountCleanupComplete;

    /// <summary>
    /// The id of the last song whose play count revisions were cleaned up.
    /// </summary>
    private long _playCountCleanupCursor;

    /// <summary>
    /// How long the worker waits once the queue has nothing to process, and how long a failed song waits for its
    /// next attempt.
    /// </summary>
    private int IntervalSeconds =>
        config.Value.SongHistoryWorkerIntervalSeconds > 0 ? config.Value.SongHistoryWorkerIntervalSeconds : 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Value.SongHistoryWorkerEnabled)
        {
            logger.LogInformation("Song history worker disabled (SongHistoryWorkerEnabled is false)");
            return;
        }

        var intervalSeconds = IntervalSeconds;

        logger.LogInformation("Song history worker starting (interval: {Interval}s)", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var foundWork = false;

            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
                var diffService = scope.ServiceProvider.GetRequiredService<ISongHistoryDiffService>();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<ISongHistoryThumbnailService>();
                var snapshotService = scope.ServiceProvider.GetRequiredService<ISongHistorySnapshotService>();

                var (processed, failed) = await ProcessQueueAsync(context, diffService, thumbnailService,
                    snapshotService, stoppingToken);
                foundWork = processed + failed > 0;

                if (!_baselineBackfillComplete)
                {
                    var backfillService = scope.ServiceProvider
                        .GetRequiredService<ISongHistoryBaselineBackfillService>();
                    await BackfillBaselinesAsync(backfillService, stoppingToken);
                }

                // The baseline backfill reverts every revision, play count ones included, so they stay until it is done
                if (_baselineBackfillComplete && !_playCountCleanupComplete)
                {
                    var cleanupService = scope.ServiceProvider
                        .GetRequiredService<ISongHistoryPlayCountCleanupService>();
                    await CleanupPlayCountRevisionsAsync(cleanupService, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during song history worker cycle");

                // Waits even if songs were processed, so an error that keeps happening is not hit in a hot loop
                foundWork = false;
            }

            // More songs may be waiting: failed ones are left out until the interval passes, so the cycles that
            // follow end up finding nothing and waiting
            if (foundWork)
            {
                continue;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Song history worker stopped");
    }

    /// <summary>
    /// Queued entries are those still pending a retry; failed ones are dead-lettered. Processed entries are removed
    /// from the queue, so they are not counted.
    /// </summary>
    public async Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId,
        CancellationToken cancellationToken)
    {
        var pending = OwnedPendingEntries(db, userId);

        var queued = await pending.CountAsync(q => q.ErrorCount < MaxErrorCount, cancellationToken);
        var failed = await pending.CountAsync(q => q.ErrorCount >= MaxErrorCount, cancellationToken);

        return new BackgroundJobCounters(queued, null, failed);
    }

    public Task<BackgroundJobFailurePage> GetFailuresAsync(MusicDbContext db, long userId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var deadLettered = OwnedPendingEntries(db, userId)
            .Where(q => q.ErrorCount >= MaxErrorCount)
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.SongId,
                SongTitle = db.Songs.Where(s => s.Id == q.SongId).Select(s => s.Title).First(),
                q.SongRevision,
                q.TransactionId,
                q.ErrorCount,
                q.CreatedAt,
                q.LastError,
            });

        return BackgroundJobFailurePage.FromQueryAsync(deadLettered, page, pageSize, q => new BackgroundJobFailure(
            q.Id.ToString(CultureInfo.InvariantCulture),
            $"{q.SongTitle} (revision {q.SongRevision})",
            q.LastError,
            q.CreatedAt,
            [
                new("QueueEntryId", q.Id.ToString(CultureInfo.InvariantCulture)),
                new("SongId", q.SongId.ToString(CultureInfo.InvariantCulture)),
                new("SongTitle", q.SongTitle),
                new("SongRevision", q.SongRevision.ToString(CultureInfo.InvariantCulture)),
                new("TransactionId", q.TransactionId?.ToString(CultureInfo.InvariantCulture)),
                new("ErrorCount", q.ErrorCount.ToString(CultureInfo.InvariantCulture)),
                new("CreatedAt", q.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
            ]), cancellationToken);
    }

    /// <summary>
    /// Unprocessed queue entries of the user's songs. Queue entries have no FK to their song, so entries of songs that
    /// no longer exist are left out.
    /// </summary>
    private static IQueryable<SongHistoryQueue> OwnedPendingEntries(MusicDbContext db, long userId) =>
        db.SongHistoryQueues
            .Where(q => q.ProcessedAt == null && db.Songs.Any(s => s.Id == q.SongId && s.OwnerId == userId));

    private async Task BackfillBaselinesAsync(
        ISongHistoryBaselineBackfillService backfillService,
        CancellationToken cancellationToken)
    {
        var batchSize = config.Value.SongHistoryBaselineBatchSize;
        if (batchSize <= 0)
        {
            batchSize = 50;
        }

        using var activity = ActivitySource.StartActivity("SongHistoryWorker.BackfillBaselines");

        var (candidates, recorded) = await backfillService.BackfillBatchAsync(batchSize, cancellationToken);

        activity?.SetTag("backfill.candidates", candidates);
        activity?.SetTag("backfill.recorded", recorded);

        if (candidates == 0)
        {
            _baselineBackfillComplete = true;
            logger.LogInformation("Song history baseline backfill complete");
        }
        else
        {
            logger.LogInformation("Song history baseline backfill: recorded {Recorded} of {Candidates} songs",
                recorded, candidates);
        }
    }

    private async Task CleanupPlayCountRevisionsAsync(
        ISongHistoryPlayCountCleanupService cleanupService,
        CancellationToken cancellationToken)
    {
        var batchSize = config.Value.SongHistoryPlayCountCleanupBatchSize;
        if (batchSize <= 0)
        {
            batchSize = 50;
        }

        using var activity = ActivitySource.StartActivity("SongHistoryWorker.CleanupPlayCountRevisions");

        var (songs, removed, nextSongId) = await cleanupService.CleanupBatchAsync(_playCountCleanupCursor,
            batchSize, cancellationToken);

        activity?.SetTag("cleanup.songs", songs);
        activity?.SetTag("cleanup.removed", removed);

        if (nextSongId is { } songId)
        {
            _playCountCleanupCursor = songId;
            logger.LogInformation(
                "Song history play count cleanup: removed {Removed} revisions from {Songs} songs (up to song {SongId})",
                removed, songs, songId);
        }
        else
        {
            _playCountCleanupComplete = true;
            logger.LogInformation("Song history play count cleanup complete");
        }
    }

    /// <summary>
    /// Pulls a batch of unprocessed queue entries and writes them to
    /// <c>song_history</c>. Exposed as a public method so tests can drive a
    /// single cycle deterministically without running the full polling loop.
    /// <para>
    /// Batches by distinct <c>SongId</c> (up to the batch size). For each song,
    /// loads all pending queue rows, groups by <c>TransactionId</c> (null → each
    /// row is its own group), selects the lowest-revision row per group as the
    /// representative pre-transaction snapshot, sorts groups by
    /// <c>SongRevision</c> ascending, and computes deltas newest → oldest.
    /// </para>
    /// </summary>
    public async Task<(int processed, int failed)> ProcessQueueAsync(
        MusicDbContext context,
        ISongHistoryDiffService diffService,
        ISongHistoryThumbnailService thumbnailService,
        ISongHistorySnapshotService snapshotService,
        CancellationToken cancellationToken)
    {
        var batchSize = config.Value.SongHistoryWorkerBatchSize;
        if (batchSize <= 0)
        {
            batchSize = 50;
        }

        // A song's entries are processed together, so one that failed recently holds back the whole song
        var retryCutoff = DateTime.UtcNow.AddSeconds(-IntervalSeconds);

        var songIds = await context.SongHistoryQueues
            .Where(q => q.ProcessedAt == null && q.ErrorCount < MaxErrorCount)
            .Where(q => !context.SongHistoryQueues.Any(f =>
                f.SongId == q.SongId && f.ProcessedAt == null && f.LastErrorAt > retryCutoff))
            .Select(q => q.SongId)
            .Distinct()
            .OrderBy(id => id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (songIds.Count == 0)
        {
            return (0, 0);
        }

        var batchEntries = await context.SongHistoryQueues
            .Where(q => q.ProcessedAt == null && q.ErrorCount < MaxErrorCount
                && songIds.Contains(q.SongId))
            .OrderBy(q => q.SongId)
            .ThenBy(q => q.SongRevision)
            .ToListAsync(cancellationToken);

        if (batchEntries.Count == 0)
        {
            return (0, 0);
        }

        var processed = 0;
        var failed = 0;

        using var cycleActivity = ActivitySource.StartActivity("SongHistoryWorker.ProcessQueue");
        cycleActivity?.SetTag("queue.batch.songs", songIds.Count);
        cycleActivity?.SetTag("queue.batch.entries", batchEntries.Count);

        var bySong = batchEntries.GroupBy(e => e.SongId).OrderBy(g => g.Key);

        foreach (var songGroup in bySong)
        {
            var songEntries = songGroup.ToList();

            using var songActivity = ActivitySource.StartActivity("SongHistoryWorker.ProcessSong");
            songActivity?.SetTag("queue.song.id", songGroup.Key);
            songActivity?.SetTag("queue.song.entry_count", songEntries.Count);

            try
            {
                await ProcessSongAsync(
                    context,
                    diffService,
                    thumbnailService,
                    snapshotService,
                    songGroup.Key,
                    songEntries,
                    cancellationToken);
                processed++;
                notifier.Publish(songGroup.Key, SongHistoryNotificationKind.Processed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;

                // Drops the song's half-recorded history, so it is neither saved with the error counts nor carried
                // over to the next songs
                context.ChangeTracker.Clear();

                var lastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                var lastErrorAt = DateTime.UtcNow;

                foreach (var entry in songEntries)
                {
                    entry.ErrorCount++;
                    entry.LastError = lastError;
                    entry.LastErrorAt = lastErrorAt;
                }

                // Updated in place: the entries may be gone by now (their owner was deleted), which would fail a
                // tracked save and abort the cycle for every remaining song
                var entryIds = songEntries.Select(e => e.Id).ToList();
                await context.SongHistoryQueues
                    .Where(q => entryIds.Contains(q.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(q => q.ErrorCount, q => q.ErrorCount + 1)
                        .SetProperty(q => q.LastError, lastError)
                        .SetProperty(q => q.LastErrorAt, lastErrorAt), cancellationToken);

                logger.LogError(ex,
                    "Failed to process song history queue entries for song {SongId} ({EntryCount} entries, attempt {ErrorCount})",
                    songGroup.Key, songEntries.Count, songEntries[0].ErrorCount);

                songActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                if (songEntries[0].ErrorCount >= MaxErrorCount)
                {
                    logger.LogWarning(
                        "Song history queue entries for song {SongId} are now dead-lettered after {ErrorCount} failures",
                        songGroup.Key, songEntries[0].ErrorCount);
                }

                notifier.Publish(songGroup.Key, SongHistoryNotificationKind.Failed);
            }
        }

        logger.LogInformation(
            "Song history worker cycle: processed {Processed} songs, {Failed} failures",
            processed,
            failed);

        return (processed, failed);
    }

    /// <summary>
    /// Processes all pending queue entries for a single song. Groups entries by
    /// <c>TransactionId</c> (null TransactionId → each row is its own group),
    /// selects the lowest-revision row per group as the pre-transaction
    /// snapshot, sorts groups by <c>SongRevision</c> ascending, and computes
    /// deltas newest → oldest using the live state (for the newest group) or
    /// the next-older group's snapshot (for older groups).
    /// </summary>
    private static async Task ProcessSongAsync(
        MusicDbContext context,
        ISongHistoryDiffService diffService,
        ISongHistoryThumbnailService thumbnailService,
        ISongHistorySnapshotService snapshotService,
        long songId,
        List<SongHistoryQueue> entries,
        CancellationToken cancellationToken)
    {
        var groups = BuildGroups(entries);

        var orderedGroups = groups
            .OrderBy(g => g.Representative.SongRevision)
            .ToList();

        var liveState = await snapshotService.GetCurrentSnapshotAsync(songId, includeCover: true, cancellationToken);

        var deltas = new SongHistoryDelta[orderedGroups.Count];

        for (var i = orderedGroups.Count - 1; i >= 0; i--)
        {
            var groupSnapshot = orderedGroups[i].Representative.Data;

            SongSnapshot? newerState;
            if (i == orderedGroups.Count - 1)
            {
                newerState = liveState;
            }
            else
            {
                newerState = orderedGroups[i + 1].Representative.Data;
            }

            if (groupSnapshot.Action == SongHistoryEntity.CreatedAction && newerState is not null)
            {
                // The transaction that created the song: its baseline is the song's full state right after it,
                // with no previous values (the trigger's own snapshot is ignored)
                var createdState = await WithCoverAsync(newerState, snapshotService, cancellationToken);
                deltas[i] = diffService.ComputeBaseline(createdState);
                continue;
            }

            var delta = diffService.ComputeDiff(groupSnapshot, newerState);
            delta = delta with { Action = groupSnapshot.Action };
            deltas[i] = delta;
        }

        var maxHistoryRevision = await context.SongHistories
            .Where(h => h.SongId == songId)
            .Select(h => (int?)h.SongRevision)
            .MaxAsync(cancellationToken) ?? 0;

        var allQueueEntries = orderedGroups.SelectMany(g => g.Entries).ToList();

        for (var i = 0; i < orderedGroups.Count; i++)
        {
            var delta = deltas[i];

            delta = ReplaceCoverWithThumbnailInDelta(delta, thumbnailService);

            var history = new SongHistoryEntity
            {
                SongId = songId,
                OwnerId = orderedGroups[i].Representative.OwnerId,
                SongRevision = maxHistoryRevision + 1 + i,
                Diff = delta,
                DiffFormat = "delta",
                Action = delta.Action ?? SongHistoryEntity.UpdatedAction,
                CreatedAt = DateTime.UtcNow,
            };

            context.SongHistories.Add(history);
        }

        context.SongHistoryQueues.RemoveRange(allQueueEntries);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Queue snapshots only embed the cover object when <c>cover_id</c> changed, so a snapshot used as a baseline may
    /// reference a cover without carrying it; loads it so the baseline records the cover too.
    /// </summary>
    private static async Task<SongSnapshot> WithCoverAsync(
        SongSnapshot snapshot,
        ISongHistorySnapshotService snapshotService,
        CancellationToken cancellationToken)
    {
        if (snapshot.Cover is not null || snapshot.CoverId is not { } coverId)
        {
            return snapshot;
        }

        return snapshot with { Cover = await snapshotService.GetCoverAsync(coverId, cancellationToken) };
    }

    /// <summary>
    /// Groups queue entries by <c>TransactionId</c>. Entries with a null
    /// <c>TransactionId</c> become individual single-entry groups. The
    /// representative (lowest-revision) row of each group provides the
    /// pre-transaction snapshot.
    /// </summary>
    private static List<QueueGroup> BuildGroups(List<SongHistoryQueue> entries)
    {
        var groups = new List<QueueGroup>();

        var txnGroups = entries
            .Where(e => e.TransactionId != null)
            .GroupBy(e => new { e.SongId, e.TransactionId })
            .Select(g =>
            {
                var ordered = g.OrderBy(e => e.SongRevision).ToList();
                return new QueueGroup(ordered[0], ordered);
            });
        groups.AddRange(txnGroups);

        var nullGroups = entries
            .Where(e => e.TransactionId == null)
            .OrderBy(e => e.SongId)
            .ThenBy(e => e.SongRevision)
            .Select(e => new QueueGroup(e, [e]));
        groups.AddRange(nullGroups);

        return groups;
    }

    /// <summary>
    /// Represents a group of queue entries sharing a <c>TransactionId</c>
    /// (or a single entry for null TransactionId). The representative row is
    /// the lowest-revision entry, whose <c>Data</c> is the pre-transaction
    /// snapshot.
    /// </summary>
    private sealed record QueueGroup(SongHistoryQueue Representative, List<SongHistoryQueue> Entries);

    /// <summary>
    /// Applies thumbnail replacement to the delta's cover <c>FieldChange</c>
    /// (both <c>Old</c> and <c>New</c> cover <c>Data</c>) before inserting. If
    /// the cover field change is null (no cover change), the delta is returned
    /// unchanged.
    /// </summary>
    internal static SongHistoryDelta ReplaceCoverWithThumbnailInDelta(
        SongHistoryDelta delta,
        ISongHistoryThumbnailService thumbnailService)
    {
        if (delta.Cover is null)
        {
            return delta;
        }

        var oldCover = ReplaceCoverDataWithThumbnail(delta.Cover.Old, thumbnailService);
        var newCover = ReplaceCoverDataWithThumbnail(delta.Cover.New, thumbnailService);

        return delta with
        {
            Cover = new FieldChange<SongSnapshotCover?>
            {
                Old = oldCover,
                New = newCover,
            },
        };
    }

    private static SongSnapshotCover? ReplaceCoverDataWithThumbnail(
        SongSnapshotCover? cover,
        ISongHistoryThumbnailService thumbnailService)
    {
        if (cover is null)
        {
            return null;
        }

        var thumbnailBytes = thumbnailService.GenerateThumbnail(cover.Data, cover.MimeType);
        if (thumbnailBytes is { Length: > 0 })
        {
            var thumbnailBase64 = Convert.ToBase64String(thumbnailBytes);
            return cover with { Data = thumbnailBase64 };
        }

        return cover with { Data = string.Empty };
    }
}