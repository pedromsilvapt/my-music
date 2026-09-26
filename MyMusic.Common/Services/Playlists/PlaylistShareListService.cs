using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Services.Playlists;

public class PlaylistShareListService : IPlaylistShareListService
{
    public async Task<List<PlaylistShareDto>> ListSharesAsync(
        MusicDbContext db,
        long[] playlistIds,
        long currentUserId,
        CancellationToken ct)
    {
        if (playlistIds.Length == 0)
            return [];

        await PlaylistOwnership.EnsureOwnerOfAllAsync(db, playlistIds, currentUserId, ct);

        return await db.PlaylistSharings
            .Where(ps => playlistIds.Contains(ps.PlaylistId))
            .Select(ps => new PlaylistShareDto
            {
                Id = ps.Id,
                PlaylistId = ps.PlaylistId,
                UserId = ps.UserId,
                Username = ps.User.Username,
                CreatedAt = ps.CreatedAt,
            })
            .ToListAsync(ct);
    }
}
