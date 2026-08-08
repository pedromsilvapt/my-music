using MyMusic.Common.Models;

namespace MyMusic.Common.Services;

/// <summary>
/// Imports a song that has been shared with the current user (a <see cref="Entities.SongSharing"/>
/// row exists for <c>(SongId, UserId == currentUser)</c>) into the recipient's own library,
/// reusing the existing <see cref="IMusicService.ImportRepositorySongs"/> pipeline.
///
/// The recipient ends up with both the shared (read-only) view and an owned copy they control.
/// The <see cref="Entities.SongSharing"/> row is intentionally NOT removed by this operation
/// (the client decides which view to show). No <see cref="Entities.SongDevice"/> rows are
/// created — this mirrors the normal <c>SongsController.Upload</c> behavior.
/// </summary>
public interface ISharedSongImportService
{
    /// <summary>
    /// Imports the shared song <paramref name="songId"/> into the <paramref name="currentUserId"/>'s library.
    /// </summary>
    /// <param name="db">The current <see cref="MusicDbContext"/> (the caller's request-scoped instance).</param>
    /// <param name="songId">The Id of the shared (source) song. May be owned by another user.</param>
    /// <param name="currentUserId">The recipient user Id performing the import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="ImportSharedSongResult"/> describing the outcome.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no song exists with <paramref name="songId"/> (maps to HTTP 404).</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the song is not shared with <paramref name="currentUserId"/> (maps to HTTP 403).</exception>
    Task<ImportSharedSongResult> ImportAsync(
        MusicDbContext db,
        long songId,
        long currentUserId,
        CancellationToken ct);
}