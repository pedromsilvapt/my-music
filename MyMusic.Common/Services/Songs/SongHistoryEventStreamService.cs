using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common.Services.SongHistory;

namespace MyMusic.Common.Services.Songs;

public class SongHistoryEventStreamService(
    MusicDbContext db,
    ICurrentUser currentUser,
    ISongHistoryPendingService pendingService,
    ISongHistoryNotifier notifier,
    IOptions<Config> config) : ISongHistoryEventStreamService
{
    /// <inheritdoc />
    public async IAsyncEnumerable<SongHistoryEvent> StreamAsync(
        long songId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var owned = await db.Songs
            .AnyAsync(s => s.Id == songId && s.OwnerId == currentUser.Id, cancellationToken);

        if (!owned)
        {
            yield return new SongHistoryEvent(SongHistoryEventType.Complete, songId);
            yield break;
        }

        // Subscribe before checking for pending entries, so a notification published between
        // the check and the wait is not lost.
        using var subscription = notifier.Subscribe(songId);

        while (await pendingService.HasPendingAsync(songId, cancellationToken))
        {
            // Periodically re-check even without notifications, in case processing happened
            // without us being told (e.g. entries dead-lettered or removed by another path).
            var notification = await WaitForNotificationAsync(subscription.Reader, GetFallbackInterval(), cancellationToken);

            if (notification == SongHistoryNotificationKind.Processed)
            {
                yield return new SongHistoryEvent(SongHistoryEventType.Processed, songId);
            }
        }

        yield return new SongHistoryEvent(SongHistoryEventType.Complete, songId);
    }

    private TimeSpan GetFallbackInterval()
    {
        var intervalSeconds = config.Value.SongHistoryWorkerIntervalSeconds;
        return TimeSpan.FromSeconds(Math.Max(1, intervalSeconds) * 3);
    }

    private static async Task<SongHistoryNotificationKind?> WaitForNotificationAsync(
        ChannelReader<SongHistoryNotificationKind> reader,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await reader.ReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
