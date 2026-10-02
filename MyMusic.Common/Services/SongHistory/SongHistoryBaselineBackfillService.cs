using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services.SongHistory.Models;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Records the <c>created</c> baseline of songs that existed before baselines were introduced. The baseline is the
/// song's earliest known state: its current state with every recorded revision reverted, newest to oldest. It becomes
/// revision 1, shifting the song's existing revisions up by one, so revision 1 is the baseline for every song.
/// </summary>
public class SongHistoryBaselineBackfillService(
    MusicDbContext db,
    ISongHistorySnapshotService snapshotService,
    ISongHistoryDiffService diffService,
    ISongHistoryThumbnailService thumbnailService,
    ISongHistoryNotifier notifier,
    ILogger<SongHistoryBaselineBackfillService> logger) : ISongHistoryBaselineBackfillService
{
    /// <inheritdoc />
    public async Task<(int candidates, int recorded)> BackfillBatchAsync(int batchSize,
        CancellationToken cancellationToken)
    {
        // Songs whose queue entries were dead-lettered cannot have their history trusted, so they are left alone
        var songIds = await db.Songs
            .Where(s => !db.SongHistories.Any(h => h.SongId == s.Id && h.Action == SongHistoryEntity.CreatedAction))
            .Where(s => !db.SongHistoryQueues.Any(q => q.SongId == s.Id && q.ProcessedAt == null
                                                       && q.ErrorCount >= SongHistoryWorker.MaxErrorCount))
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var recorded = 0;

        foreach (var songId in songIds)
        {
            try
            {
                if (await BackfillSongAsync(songId, cancellationToken))
                {
                    recorded++;
                    notifier.Publish(songId, SongHistoryNotificationKind.Processed);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record the history baseline of song {SongId}", songId);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return (songIds.Count, recorded);
    }

    private async Task<bool> BackfillSongAsync(long songId, CancellationToken cancellationToken)
    {
        var isNpgsql = db.Database.IsNpgsql();

        // Repeatable read keeps the pending check, the history and the current state consistent with each other
        await using var transaction = isNpgsql
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : await db.Database.BeginTransactionAsync(cancellationToken);

        if (isNpgsql)
        {
            // Holds off edits of the song itself until its baseline is in place
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM songs WHERE id = {songId} FOR UPDATE",
                cancellationToken);
        }

        var song = await db.Songs
            .Where(s => s.Id == songId)
            .Select(s => new
            {
                s.CreatedAt,
                HasBaseline = db.SongHistories.Any(h =>
                    h.SongId == s.Id && h.Action == SongHistoryEntity.CreatedAction),
                HasPendingChanges = db.SongHistoryQueues.Any(q => q.SongId == s.Id && q.ProcessedAt == null),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Pending changes are not in the history yet, so the current state cannot be reverted back to the baseline
        if (song is null || song.HasBaseline || song.HasPendingChanges)
        {
            return false;
        }

        var currentState = await snapshotService.GetCurrentSnapshotAsync(songId, includeCover: true,
            cancellationToken);

        if (currentState is null)
        {
            return false;
        }

        var history = await db.SongHistories
            .AsNoTracking()
            .Where(h => h.SongId == songId)
            .OrderByDescending(h => h.SongRevision)
            .ToListAsync(cancellationToken);

        var baselineState = currentState;
        var coverIsThumbnail = false;

        foreach (var revision in history.Where(h => h.DiffFormat == "delta"))
        {
            baselineState = SongHistoryDeltaReverter.Revert(baselineState, revision.Diff);

            // Covers recorded in history were already reduced to thumbnails
            if (revision.Diff.Cover is not null)
            {
                coverIsThumbnail = true;
            }
        }

        if (baselineState is { Cover: null, CoverId: { } coverId })
        {
            baselineState = baselineState with
            {
                Cover = await snapshotService.GetCoverAsync(coverId, cancellationToken),
            };
            coverIsThumbnail = false;
        }

        var baseline = diffService.ComputeBaseline(baselineState);

        if (!coverIsThumbnail)
        {
            baseline = SongHistoryWorker.ReplaceCoverWithThumbnailInDelta(baseline, thumbnailService);
        }

        // Make room for the baseline at revision 1, going through negative revisions so no two rows ever collide
        await db.SongHistories
            .Where(h => h.SongId == songId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.SongRevision, h => -h.SongRevision - 1), cancellationToken);
        await db.SongHistories
            .Where(h => h.SongId == songId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.SongRevision, h => -h.SongRevision), cancellationToken);

        db.SongHistories.Add(new SongHistoryEntity
        {
            SongId = songId,
            SongRevision = 1,
            Diff = baseline,
            DiffFormat = "delta",
            Action = SongHistoryEntity.CreatedAction,
            CreatedAt = DateTime.SpecifyKind(song.CreatedAt, DateTimeKind.Utc),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogDebug("Recorded the history baseline of song {SongId} ({RevisionCount} existing revisions)",
            songId, history.Count);

        return true;
    }
}
