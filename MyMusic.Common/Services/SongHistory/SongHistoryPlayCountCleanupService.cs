using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services.SongHistory.Models;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Removes the revisions whose only change is <c>play_count</c>, left over from when every play was recorded in the
/// song history, and renumbers each cleaned song's remaining revisions to 1..n in their original order. Only meant to
/// run once every song has its <c>created</c> baseline, since the baseline backfill reverts these revisions too.
/// </summary>
public class SongHistoryPlayCountCleanupService(
    MusicDbContext db,
    ISongHistoryNotifier notifier,
    ILogger<SongHistoryPlayCountCleanupService> logger) : ISongHistoryPlayCountCleanupService
{
    private static readonly SongHistoryDelta EmptyDelta = new();

    /// <inheritdoc />
    public async Task<(int songs, int removed, long? nextSongId)> CleanupBatchAsync(long afterSongId, int batchSize,
        CancellationToken cancellationToken)
    {
        // New revisions never carry play_count, so only legacy ones match; songs whose play_count changes were mixed
        // with real ones keep matching, hence paging by song id instead of looping until nothing matches
        var songIds = await RevisionsMentioningPlayCount()
            .Where(h => h.Action == SongHistoryEntity.UpdatedAction && h.SongId > afterSongId)
            .Select(h => h.SongId)
            .Distinct()
            .OrderBy(id => id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var removed = 0;

        foreach (var songId in songIds)
        {
            try
            {
                var songRemoved = await CleanupSongAsync(songId, cancellationToken);
                if (songRemoved > 0)
                {
                    removed += songRemoved;
                    notifier.Publish(songId, SongHistoryNotificationKind.Processed);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove the play count revisions of song {SongId}", songId);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return (songIds.Count, removed, songIds.Count > 0 ? songIds[^1] : null);
    }

    /// <summary>
    /// Whether <paramref name="delta"/> records nothing but a play count change.
    /// </summary>
    internal static bool IsPlayCountOnly(SongHistoryDelta delta) =>
        delta.Action is null or SongHistoryEntity.UpdatedAction
        && delta.PlayCount is not null
        && delta with { Action = null, PlayCount = null } == EmptyDelta;

    /// <summary>
    /// The diff column holds serialized JSON behind a value converter, so the text match is done in SQL.
    /// </summary>
    private IQueryable<SongHistoryEntity> RevisionsMentioningPlayCount()
    {
        var entityType = db.Model.FindEntityType(typeof(SongHistoryEntity))!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var sql = db.GetService<ISqlGenerationHelper>();

        var tableName = sql.DelimitIdentifier(table.Name, table.Schema);
        var diffColumn = sql.DelimitIdentifier(
            entityType.FindProperty(nameof(SongHistoryEntity.Diff))!.GetColumnName(table)!);

        return db.SongHistories.FromSqlRaw($"SELECT * FROM {tableName} WHERE {diffColumn} LIKE {{0}}",
            "%\"play_count\"%");
    }

    private async Task<int> CleanupSongAsync(long songId, CancellationToken cancellationToken)
    {
        var isNpgsql = db.Database.IsNpgsql();

        await using var transaction = isNpgsql
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : await db.Database.BeginTransactionAsync(cancellationToken);

        if (isNpgsql)
        {
            // Holds off edits of the song itself while its revisions are renumbered
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM songs WHERE id = {songId} FOR UPDATE",
                cancellationToken);
        }

        var history = await db.SongHistories
            .Where(h => h.SongId == songId)
            .OrderBy(h => h.SongRevision)
            .ToListAsync(cancellationToken);

        var playCountOnly = history.Where(h => IsPlayCountOnly(h.Diff)).ToList();

        if (playCountOnly.Count == 0)
        {
            return 0;
        }

        db.SongHistories.RemoveRange(playCountOnly);

        // Renumber through negative revisions so no two rows ever collide on the unique (song, revision) index
        var renumbered = history.Except(playCountOnly)
            .Select((revision, index) => (revision, newRevision: index + 1))
            .Where(r => r.revision.SongRevision != r.newRevision)
            .ToList();

        foreach (var (revision, newRevision) in renumbered)
        {
            revision.SongRevision = -newRevision;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (revision, newRevision) in renumbered)
        {
            revision.SongRevision = newRevision;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogDebug("Removed {Removed} play count revisions of song {SongId}", playCountOnly.Count, songId);

        return playCountOnly.Count;
    }
}
