using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public class SongShareService : ISongShareService
{
    public async Task<List<SongShareDto>> ListSharesAsync(
        MusicDbContext db,
        long songId,
        long currentUserId,
        CancellationToken ct)
    {
        var song = await db.Songs
            .Where(s => s.Id == songId)
            .Select(s => new { s.OwnerId })
            .FirstOrDefaultAsync(ct);

        if (song == null)
            throw new InvalidOperationException($"Song not found with id {songId}");

        if (song.OwnerId != currentUserId)
            throw new UnauthorizedAccessException($"User {currentUserId} does not own song {songId}");

        return await db.SongSharings
            .Where(ss => ss.SongId == songId)
            .Select(ss => new SongShareDto
            {
                Id = ss.Id,
                SongId = ss.SongId,
                UserId = ss.UserId,
                Username = ss.User.Username,
                CreatedAt = ss.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<long> CreateShareAsync(
        MusicDbContext db,
        long songId,
        long targetUserId,
        long currentUserId,
        CancellationToken ct)
    {
        var song = await db.Songs
            .Where(s => s.Id == songId)
            .Select(s => new { s.OwnerId })
            .FirstOrDefaultAsync(ct);

        if (song == null)
            throw new InvalidOperationException($"Song not found with id {songId}");

        if (song.OwnerId != currentUserId)
            throw new UnauthorizedAccessException($"User {currentUserId} does not own song {songId}");

        var targetUserExists = await db.Users.AnyAsync(u => u.Id == targetUserId, ct);
        if (!targetUserExists)
            throw new InvalidOperationException($"User not found with id {targetUserId}");

        if (targetUserId == song.OwnerId)
            throw new InvalidOperationException("Cannot share a song with its owner");

        // Idempotent: if a share already exists for this (SongId, UserId) pair, return its Id.
        var existingId = await db.SongSharings
            .Where(ss => ss.SongId == songId && ss.UserId == targetUserId)
            .Select(ss => (long?)ss.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId is long id)
            return id;

        var sharing = new SongSharing
        {
            SongId = songId,
            UserId = targetUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.SongSharings.Add(sharing);
        await db.SaveChangesAsync(ct);

        return sharing.Id;
    }

    public async Task DeleteShareAsync(
        MusicDbContext db,
        long songId,
        long targetUserId,
        long currentUserId,
        CancellationToken ct)
    {
        var song = await db.Songs
            .Where(s => s.Id == songId)
            .Select(s => new { s.OwnerId })
            .FirstOrDefaultAsync(ct);

        if (song == null)
            throw new InvalidOperationException($"Song not found with id {songId}");

        if (song.OwnerId != currentUserId)
            throw new UnauthorizedAccessException($"User {currentUserId} does not own song {songId}");

        var sharing = await db.SongSharings
            .FirstOrDefaultAsync(ss => ss.SongId == songId && ss.UserId == targetUserId, ct);

        if (sharing == null)
            throw new InvalidOperationException(
                $"Share not found for song {songId} and user {targetUserId}");

        db.SongSharings.Remove(sharing);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<SongSharerDto>> ListSharersAsync(
        MusicDbContext db,
        long currentUserId,
        CancellationToken ct)
    {
        return await db.SongSharings
            .Where(ss => ss.UserId == currentUserId)
            .Select(ss => ss.Song.Owner)
            .Distinct()
            .Select(u => new SongSharerDto
            {
                Id = u.Id,
                Username = u.Username,
                Name = u.Name,
            })
            .ToListAsync(ct);
    }

    public async Task<List<SongShareDto>> ListSharesForSongsAsync(
        MusicDbContext db,
        long[] songIds,
        long currentUserId,
        CancellationToken ct)
    {
        if (songIds.Length == 0)
            return [];

        await EnsureOwnerOfAllAsync(db, songIds, currentUserId, ct);

        return await db.SongSharings
            .Where(ss => songIds.Contains(ss.SongId))
            .Select(ss => new SongShareDto
            {
                Id = ss.Id,
                SongId = ss.SongId,
                UserId = ss.UserId,
                Username = ss.User.Username,
                CreatedAt = ss.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<(int Created, int Removed)> ManageSharesAsync(
        MusicDbContext db,
        long[] songIds,
        List<SongShareAction> actions,
        long currentUserId,
        CancellationToken ct)
    {
        if (songIds.Length == 0 || actions.Count == 0)
            return (0, 0);

        await EnsureOwnerOfAllAsync(db, songIds, currentUserId, ct);

        // Validate all target users exist and are not the owner. Reuses the per-song
        // validation semantics; performed once up-front to fail fast before any mutation.
        var targetUserIds = actions.Select(a => a.UserId).Distinct().ToList();
        var existingUserIds = await db.Users
            .Where(u => targetUserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);
        var missingUserIds = targetUserIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
            throw new InvalidOperationException($"User not found with id {missingUserIds[0]}");

        if (targetUserIds.Contains(currentUserId))
            throw new InvalidOperationException("Cannot share a song with its owner");

        // Load the existing share rows for the targeted (SongId, UserId) pairs in one query
        // so the per-pair Add/Remove decisions below are idempotent without extra round-trips.
        var actionUserIds = targetUserIds;
        var existing = await db.SongSharings
            .Where(ss => songIds.Contains(ss.SongId) && actionUserIds.Contains(ss.UserId))
            .ToListAsync(ct);
        var existingByKey = existing
            .ToDictionary(ss => (ss.SongId, ss.UserId));

        var created = 0;
        var removed = 0;

        foreach (var action in actions)
        {
            foreach (var songId in songIds)
            {
                var key = (songId, action.UserId);
                existingByKey.TryGetValue(key, out var row);

                if (action.Action == SongShareActionType.Add)
                {
                    if (row is not null)
                        continue;

                    var sharing = new SongSharing
                    {
                        SongId = songId,
                        UserId = action.UserId,
                        CreatedAt = DateTime.UtcNow,
                    };
                    db.SongSharings.Add(sharing);
                    existingByKey[key] = sharing;
                    created++;
                }
                else // Remove
                {
                    if (row is null)
                        continue;

                    db.SongSharings.Remove(row);
                    existingByKey.Remove(key);
                    removed++;
                }
            }
        }

        if (created > 0 || removed > 0)
            await db.SaveChangesAsync(ct);

        return (created, removed);
    }

    /// <summary>
    /// Verifies that every song in <paramref name="songIds"/> exists and is owned by
    /// <paramref name="currentUserId"/>. Throws <see cref="InvalidOperationException"/> if a
    /// song is missing, or <see cref="UnauthorizedAccessException"/> if any song is not owned.
    /// </summary>
    private static async Task EnsureOwnerOfAllAsync(
        MusicDbContext db,
        long[] songIds,
        long currentUserId,
        CancellationToken ct)
    {
        var ownedSongIds = await db.Songs
            .Where(s => songIds.Contains(s.Id) && s.OwnerId == currentUserId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (ownedSongIds.Count != songIds.Length)
        {
            // Distinguish "not found" from "not owner" by checking which ids are missing entirely.
            var existingIds = await db.Songs
                .Where(s => songIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(ct);
            var missingIds = songIds.Except(existingIds).ToList();
            if (missingIds.Count > 0)
                throw new InvalidOperationException($"Song not found with id {missingIds[0]}");

            var notOwnedIds = songIds.Except(ownedSongIds).ToList();
            throw new UnauthorizedAccessException(
                $"User {currentUserId} does not own song {notOwnedIds[0]}");
        }
    }
}