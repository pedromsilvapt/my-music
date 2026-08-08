namespace MyMusic.Common.Services;

/// <summary>
/// Service for managing <see cref="Entities.SongSharing"/> rows (share CRUD) and
/// querying the distinct set of users who have shared songs with the current user.
/// </summary>
public interface ISongShareService
{
    /// <summary>
    /// Lists all shares for a song. Owner-only — throws <see cref="UnauthorizedAccessException"/>
    /// if <paramref name="currentUserId"/> is not the song's owner.
    /// </summary>
    Task<List<SongShareDto>> ListSharesAsync(
        MusicDbContext db,
        long songId,
        long currentUserId,
        CancellationToken ct);

    /// <summary>
    /// Creates a share of <paramref name="songId"/> with <paramref name="targetUserId"/>.
    /// Owner-only. Idempotent: if a share already exists for the (SongId, UserId) pair,
    /// returns the existing <see cref="Entities.SongSharing.Id"/> without creating a new row.
    /// </summary>
    /// <returns>The <see cref="Entities.SongSharing.Id"/> of the new or existing share.</returns>
    Task<long> CreateShareAsync(
        MusicDbContext db,
        long songId,
        long targetUserId,
        long currentUserId,
        CancellationToken ct);

    /// <summary>
    /// Removes the share of <paramref name="songId"/> with <paramref name="targetUserId"/>.
    /// Owner-only. Throws if the share row does not exist.
    /// </summary>
    Task DeleteShareAsync(
        MusicDbContext db,
        long songId,
        long targetUserId,
        long currentUserId,
        CancellationToken ct);

    /// <summary>
    /// Returns the distinct users who have shared at least one song with <paramref name="currentUserId"/>.
    /// Drives the client's "shared with me" sharer sub-menu.
    /// </summary>
    Task<List<SongSharerDto>> ListSharersAsync(
        MusicDbContext db,
        long currentUserId,
        CancellationToken ct);

    /// <summary>
    /// Lists all shares for the given songs. Owner-only — throws
    /// <see cref="UnauthorizedAccessException"/> if any song is not owned by
    /// <paramref name="currentUserId"/>. Used by the multi-song "Manage Sharing" dialog
    /// to compute per-user match counts across the selected songs.
    /// </summary>
    Task<List<SongShareDto>> ListSharesForSongsAsync(
        MusicDbContext db,
        long[] songIds,
        long currentUserId,
        CancellationToken ct);

    /// <summary>
    /// Batch upsert/delete of shares across multiple songs. Owner-only — validates every
    /// song in <paramref name="songIds"/> is owned by <paramref name="currentUserId"/> and
    /// every target user in <paramref name="actions"/> exists and is not the owner.
    /// For each <c>{ UserId, Action }</c> applied to all <paramref name="songIds"/>:
    /// <c>Add</c> is idempotent (skips if a row already exists for the (SongId, UserId) pair),
    /// <c>Remove</c> is idempotent (no error if the row is missing).
    /// </summary>
    /// <returns>The number of created and removed <see cref="Entities.SongSharing"/> rows.</returns>
    Task<(int Created, int Removed)> ManageSharesAsync(
        MusicDbContext db,
        long[] songIds,
        List<SongShareAction> actions,
        long currentUserId,
        CancellationToken ct);
}