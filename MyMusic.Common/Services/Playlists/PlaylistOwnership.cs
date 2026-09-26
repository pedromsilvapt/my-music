using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Playlists;

internal static class PlaylistOwnership
{
    /// <summary>
    /// Verifies that every playlist in <paramref name="playlistIds"/> exists, is owned by
    /// <paramref name="currentUserId"/> and is a regular (shareable) playlist. Throws
    /// <see cref="InvalidOperationException"/> if a playlist is missing or is a system playlist
    /// (Queue/Favorites), or <see cref="UnauthorizedAccessException"/> if any playlist is not owned.
    /// </summary>
    public static async Task EnsureOwnerOfAllAsync(
        MusicDbContext db,
        long[] playlistIds,
        long currentUserId,
        CancellationToken ct)
    {
        var playlists = await db.Playlists
            .Where(p => playlistIds.Contains(p.Id))
            .Select(p => new { p.Id, p.OwnerId, p.Type })
            .ToListAsync(ct);

        var missingIds = playlistIds.Except(playlists.Select(p => p.Id)).ToList();
        if (missingIds.Count > 0)
            throw new InvalidOperationException($"Playlist not found with id {missingIds[0]}");

        var notOwned = playlists.FirstOrDefault(p => p.OwnerId != currentUserId);
        if (notOwned != null)
            throw new UnauthorizedAccessException(
                $"User {currentUserId} does not own playlist {notOwned.Id}");

        var system = playlists.FirstOrDefault(p => p.Type != PlaylistType.Playlist);
        if (system != null)
            throw new InvalidOperationException(
                $"Cannot share system playlist {system.Id} ({system.Type})");
    }
}
