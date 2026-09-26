using System.Threading.Channels;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Outcome of a <see cref="SongHistoryWorker"/> attempt at processing a song's queue entries.
/// </summary>
public enum SongHistoryNotificationKind
{
    /// <summary>The song's queue entries were written to history.</summary>
    Processed,

    /// <summary>Processing failed; entries were retried or dead-lettered.</summary>
    Failed,
}

/// <summary>
/// In-process pub/sub that lets the <see cref="SongHistoryWorker"/> tell interested listeners
/// (e.g. SSE streams) when a song's history queue has been processed. Only works when the
/// worker and the listeners run in the same process.
/// </summary>
public interface ISongHistoryNotifier
{
    /// <summary>
    /// Subscribes to notifications for <paramref name="songId"/>. Dispose the returned
    /// subscription to stop receiving them.
    /// </summary>
    ISongHistorySubscription Subscribe(long songId);

    /// <summary>
    /// Notifies all current subscribers of <paramref name="songId"/>. Never blocks.
    /// </summary>
    void Publish(long songId, SongHistoryNotificationKind kind);
}

public interface ISongHistorySubscription : IDisposable
{
    ChannelReader<SongHistoryNotificationKind> Reader { get; }
}
