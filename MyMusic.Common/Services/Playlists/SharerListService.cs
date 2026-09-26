using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Services.Playlists;

public class SharerListService : ISharerListService
{
    public async Task<List<SongSharerDto>> ListSharersAsync(
        MusicDbContext db,
        long currentUserId,
        CancellationToken ct)
    {
        return await db.PlaylistSharings
            .Where(ps => ps.UserId == currentUserId)
            .Select(ps => ps.Playlist.Owner)
            .Distinct()
            .Select(u => new SongSharerDto
            {
                Id = u.Id,
                Username = u.Username,
                Name = u.Name,
            })
            .ToListAsync(ct);
    }
}
