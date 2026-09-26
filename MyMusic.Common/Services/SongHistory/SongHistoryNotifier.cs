using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace MyMusic.Common.Services.SongHistory;

/// <inheritdoc />
public class SongHistoryNotifier : ISongHistoryNotifier
{
    private const int SubscriptionCapacity = 8;

    private readonly ConcurrentDictionary<long, ImmutableList<Channel<SongHistoryNotificationKind>>> _subscribers = new();

    /// <inheritdoc />
    public ISongHistorySubscription Subscribe(long songId)
    {
        var channel = Channel.CreateBounded<SongHistoryNotificationKind>(new BoundedChannelOptions(SubscriptionCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        _subscribers.AddOrUpdate(songId, [channel], (_, list) => list.Add(channel));

        return new Subscription(this, songId, channel);
    }

    /// <inheritdoc />
    public void Publish(long songId, SongHistoryNotificationKind kind)
    {
        if (!_subscribers.TryGetValue(songId, out var channels))
        {
            return;
        }

        foreach (var channel in channels)
        {
            channel.Writer.TryWrite(kind);
        }
    }

    private void Unsubscribe(long songId, Channel<SongHistoryNotificationKind> channel)
    {
        channel.Writer.TryComplete();

        while (_subscribers.TryGetValue(songId, out var channels))
        {
            var updated = channels.Remove(channel);
            var replaced = updated.IsEmpty
                ? _subscribers.TryRemove(new KeyValuePair<long, ImmutableList<Channel<SongHistoryNotificationKind>>>(songId, channels))
                : _subscribers.TryUpdate(songId, updated, channels);

            if (replaced)
            {
                return;
            }
        }
    }

    private sealed class Subscription(
        SongHistoryNotifier notifier,
        long songId,
        Channel<SongHistoryNotificationKind> channel) : ISongHistorySubscription
    {
        private int _disposed;

        public ChannelReader<SongHistoryNotificationKind> Reader => channel.Reader;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                notifier.Unsubscribe(songId, channel);
            }
        }
    }
}
