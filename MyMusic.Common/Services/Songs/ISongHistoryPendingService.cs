namespace MyMusic.Common.Services.Songs;

public interface ISongHistoryPendingService
{
    /// <summary>
    /// Returns whether the song still has queue entries waiting to be turned into history rows
    /// by the <see cref="SongHistory.SongHistoryWorker"/>. Dead-lettered entries (those that
    /// reached <see cref="SongHistory.SongHistoryWorker.MaxErrorCount"/>) are not considered
    /// pending, since they will never be processed. Does not check ownership.
    /// </summary>
    Task<bool> HasPendingAsync(long songId, CancellationToken cancellationToken = default);
}
