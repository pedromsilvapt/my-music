namespace MyMusic.Common.Services.Playlists;

/// <summary>
/// Lists the users who have shared at least one playlist with the current user.
/// </summary>
public interface ISharerListService
{
    /// <summary>
    /// Returns the distinct owners of playlists shared with <paramref name="currentUserId"/>.
    /// Drives the client's "shared with me" sharer sub-menu.
    /// </summary>
    Task<List<SongSharerDto>> ListSharersAsync(
        MusicDbContext db,
        long currentUserId,
        CancellationToken ct);
}
