namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Removes the history revisions recorded back when playing a song bumped its play count, which hold no change other
/// than <c>play_count</c>, and compacts the remaining revisions so they stay numbered 1..n.
/// </summary>
public interface ISongHistoryPlayCountCleanupService
{
    /// <summary>
    /// Cleans up to <paramref name="batchSize"/> songs with an id greater than <paramref name="afterSongId"/> whose
    /// history still mentions <c>play_count</c>. Returns how many such songs were found, how many revisions were
    /// removed, and the song id to continue after (<c>null</c> once no song is left).
    /// </summary>
    Task<(int songs, int removed, long? nextSongId)> CleanupBatchAsync(long afterSongId, int batchSize,
        CancellationToken cancellationToken);
}
