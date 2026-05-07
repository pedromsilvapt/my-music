namespace MyMusic.Common.Services.PlaylistSongs;

public interface IPlaylistSongSkipService
{
    /// <summary>
    /// Sets the skip-next-playback flag on a single playlist song.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Playlist or song not found.</exception>
    /// <exception cref="UnauthorizedAccessException">User does not own the playlist.</exception>
    Task SetSkipNextPlayback(MusicDbContext db, long playlistId, long songId, bool skipNextPlayback, long userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the skip-next-playback flag on multiple playlist songs in a batch.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Playlist not found.</exception>
    /// <exception cref="ArgumentException">Invalid input parameters.</exception>
    /// <exception cref="UnauthorizedAccessException">User does not own the playlist.</exception>
    Task BatchSetSkipNextPlayback(MusicDbContext db, long playlistId, List<long> songIds, bool skipNextPlayback,
        long userId, CancellationToken cancellationToken = default);
}
