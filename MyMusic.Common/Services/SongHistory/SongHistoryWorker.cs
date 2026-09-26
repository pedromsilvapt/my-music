using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory.Models;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Polling background service that drains the <c>song_history_queues</c> table.
/// Each queue row (written by a DB trigger on song/album/artist/genre/cover
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
/// Rows that fail <see cref="MaxErrorCount"/> times
/// (default 3) are dead-lettered: left in the queue with <c>ErrorCount &gt;= 3</c>
/// and never retried. The <c>songs</c> BEFORE DELETE trigger blocks deletion
/// of a song that still has dead-lettered queue entries.
/// </para>
/// </summary>
public class SongHistoryWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<Config> config,
    ISongHistoryNotifier notifier,
    ILogger<SongHistoryWorker> logger) : BackgroundService
{
    /// <summary>
    /// Maximum number of processing attempts before a queue entry is dead-lettered.
    /// </summary>
    public const int MaxErrorCount = 3;

    private static readonly ActivitySource ActivitySource = new("MyMusic.SongHistoryWorker");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Value.SongHistoryWorkerEnabled)
        {
            logger.LogInformation("Song history worker disabled (SongHistoryWorkerEnabled is false)");
            return;
        }

        var intervalSeconds = config.Value.SongHistoryWorkerIntervalSeconds;
        if (intervalSeconds <= 0)
        {
            intervalSeconds = 10;
        }

        logger.LogInformation("Song history worker starting (interval: {Interval}s)", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
                var diffService = scope.ServiceProvider.GetRequiredService<ISongHistoryDiffService>();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<ISongHistoryThumbnailService>();
                var snapshotService = scope.ServiceProvider.GetRequiredService<ISongHistorySnapshotService>();

                await ProcessQueueAsync(context, diffService, thumbnailService, snapshotService, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during song history worker cycle");
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

        var songIds = await context.SongHistoryQueues
            .Where(q => q.ProcessedAt == null && q.ErrorCount < MaxErrorCount)
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

                foreach (var entry in songEntries)
                {
                    entry.ErrorCount++;
                    entry.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                }

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

                await context.SaveChangesAsync(cancellationToken);
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
                SongRevision = maxHistoryRevision + 1 + i,
                Diff = delta,
                DiffFormat = "delta",
                CreatedAt = DateTime.UtcNow,
            };

            context.SongHistories.Add(history);
        }

        context.SongHistoryQueues.RemoveRange(allQueueEntries);

        await context.SaveChangesAsync(cancellationToken);
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