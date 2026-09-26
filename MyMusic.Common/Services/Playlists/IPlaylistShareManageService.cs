namespace MyMusic.Common.Services.Playlists;

/// <summary>
/// Batch add/remove of <see cref="Entities.PlaylistSharing"/> rows.
/// </summary>
public interface IPlaylistShareManageService
{
    /// <summary>
    /// Batch upsert/delete of shares across multiple playlists. Owner-only — validates every
    /// playlist in <paramref name="playlistIds"/> is a regular playlist owned by
    /// <paramref name="currentUserId"/>, and every target user in <paramref name="actions"/>
    /// exists and is not the owner. For each <c>{ UserId, Action }</c> applied to all playlists:
    /// <c>Add</c> is idempotent (skips if a row already exists for the (PlaylistId, UserId) pair),
    /// <c>Remove</c> is idempotent (no error if the row is missing).
    /// </summary>
    /// <returns>The number of created and removed <see cref="Entities.PlaylistSharing"/> rows.</returns>
    Task<(int Created, int Removed)> ManageSharesAsync(
        MusicDbContext db,
        long[] playlistIds,
        List<PlaylistShareAction> actions,
        long currentUserId,
        CancellationToken ct);
}
