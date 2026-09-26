using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Playlists;

public class PlaylistShareManageService : IPlaylistShareManageService
{
    public async Task<(int Created, int Removed)> ManageSharesAsync(
        MusicDbContext db,
        long[] playlistIds,
        List<PlaylistShareAction> actions,
        long currentUserId,
        CancellationToken ct)
    {
        if (playlistIds.Length == 0 || actions.Count == 0)
            return (0, 0);

        await PlaylistOwnership.EnsureOwnerOfAllAsync(db, playlistIds, currentUserId, ct);

        // Validate all target users exist and are not the owner, up-front to fail fast
        // before any mutation.
        var targetUserIds = actions.Select(a => a.UserId).Distinct().ToList();
        var existingUserIds = await db.Users
            .Where(u => targetUserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);
        var missingUserIds = targetUserIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
            throw new InvalidOperationException($"User not found with id {missingUserIds[0]}");

        if (targetUserIds.Contains(currentUserId))
            throw new InvalidOperationException("Cannot share a playlist with its owner");

        // Load the existing share rows for the targeted (PlaylistId, UserId) pairs in one query
        // so the per-pair Add/Remove decisions below are idempotent without extra round-trips.
        var existingByKey = await db.PlaylistSharings
            .Where(ps => playlistIds.Contains(ps.PlaylistId) && targetUserIds.Contains(ps.UserId))
            .ToDictionaryAsync(ps => (ps.PlaylistId, ps.UserId), ct);

        var created = 0;
        var removed = 0;

        foreach (var action in actions)
        {
            foreach (var playlistId in playlistIds)
            {
                var key = (playlistId, action.UserId);
                existingByKey.TryGetValue(key, out var row);

                if (action.Action == PlaylistShareActionType.Add)
                {
                    if (row is not null)
                        continue;

                    var sharing = new PlaylistSharing
                    {
                        PlaylistId = playlistId,
                        UserId = action.UserId,
                        CreatedAt = DateTime.UtcNow,
                    };
                    db.PlaylistSharings.Add(sharing);
                    existingByKey[key] = sharing;
                    created++;
                }
                else // Remove
                {
                    if (row is null)
                        continue;

                    db.PlaylistSharings.Remove(row);
                    existingByKey.Remove(key);
                    removed++;
                }
            }
        }

        if (created > 0 || removed > 0)
            await db.SaveChangesAsync(ct);

        return (created, removed);
    }
}
