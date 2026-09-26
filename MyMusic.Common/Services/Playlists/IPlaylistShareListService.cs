namespace MyMusic.Common.Services.Playlists;

/// <summary>
/// Lists the <see cref="Entities.PlaylistSharing"/> rows of a set of playlists.
/// </summary>
public interface IPlaylistShareListService
{
    /// <summary>
    /// Lists all shares for the given playlists. Owner-only — throws
    /// <see cref="UnauthorizedAccessException"/> if any playlist is not owned by
    /// <paramref name="currentUserId"/>. Used by the "Share Playlists" dialog to compute
    /// per-user match counts across the selected playlists.
    /// </summary>
    Task<List<PlaylistShareDto>> ListSharesAsync(
        MusicDbContext db,
        long[] playlistIds,
        long currentUserId,
        CancellationToken ct);
}
