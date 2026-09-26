namespace MyMusic.Common.Services.Songs;

public enum SongHistoryEventType
{
    /// <summary>New history rows were written for the song.</summary>
    Processed,

    /// <summary>The song has no more pending history; the stream ends after this event.</summary>
    Complete,
}

public record SongHistoryEvent(SongHistoryEventType Type, long SongId);

public interface ISongHistoryEventStreamService
{
    /// <summary>
    /// Streams history processing events for a song owned by the current user. Yields
    /// <see cref="SongHistoryEventType.Processed"/> each time the history worker processes the
    /// song, and ends with a single <see cref="SongHistoryEventType.Complete"/> once the song has
    /// no pending history left. Songs that don't exist or aren't owned by the current user
    /// complete immediately.
    /// <para>
    /// Relies on the in-process <see cref="SongHistory.ISongHistoryNotifier"/>, so the
    /// <see cref="SongHistory.SongHistoryWorker"/> must run in the same process as the caller.
    /// </para>
    /// </summary>
    IAsyncEnumerable<SongHistoryEvent> StreamAsync(long songId, CancellationToken cancellationToken = default);
}
