using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

public class WishlistService(
    MusicDbContext db,
    ISourcesService sourcesService,
    IOptions<Config> config,
    ILogger<WishlistService> logger) : IWishlistService
{
    public async Task<WishlistItem> CreateAsync(long userId, long sourceId, string query, List<string> songIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.WishlistItems
            .FirstOrDefaultAsync(w => w.OwnerId == userId && w.SourceId == sourceId && w.Query == query,
                cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var user = await db.Users.FindAsync([userId], cancellationToken) ??
                   throw new Exception($"User not found with id {userId}");

        var hash = ComputeHash(songIds);
        var now = DateTime.UtcNow;

        var item = new WishlistItem
        {
            Owner = user,
            OwnerId = userId,
            SourceId = sourceId,
            Query = query,
            Hash = hash,
            Status = WishlistItemStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WishlistItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task<List<WishlistItem>> ListAsync(long userId, long? sourceId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.WishlistItems
            .Include(w => w.Source)
            .Where(w => w.OwnerId == userId);

        if (sourceId.HasValue)
        {
            query = query.Where(w => w.SourceId == sourceId.Value);
        }

        return await query
            .OrderByDescending(w => w.Status == WishlistItemStatus.Updated)
            .ThenByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WishlistItem> UpdateHashAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await db.WishlistItems
            .Include(w => w.Source)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (item == null)
        {
            throw new Exception($"Wishlist item not found with id {id}");
        }

        var source = await sourcesService.GetSourceClientAsync(item.SourceId, cancellationToken);
        var songs = await source.SearchSongsAsync(item.Query, cancellationToken);
        var songIds = songs
            .Take(config.Value.WishlistMaxResultsToHash)
            .Select(s => s.Id)
            .ToList();

        item.Hash = ComputeHash(songIds);
        item.Status = WishlistItemStatus.Active;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await db.WishlistItems.FindAsync([id], cancellationToken);

        if (item != null)
        {
            db.WishlistItems.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var activeItems = await db.WishlistItems
            .Include(w => w.Source)
            .Where(w => w.Status == WishlistItemStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var item in activeItems)
        {
            try
            {
                var source = await sourcesService.GetSourceClientAsync(item.SourceId, cancellationToken);
                var songs = await source.SearchSongsAsync(item.Query, cancellationToken);
                var songIds = songs
                    .Take(config.Value.WishlistMaxResultsToHash)
                    .Select(s => s.Id)
                    .ToList();

                var newHash = ComputeHash(songIds);

                if (newHash != item.Hash)
                {
                    item.Status = WishlistItemStatus.Updated;
                }

                item.UpdatedAt = DateTime.UtcNow;
                item.ContinuousFailedCount = 0;
                item.LastErrorMessage = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check updates for wishlist item {ItemId}", item.Id);
                item.ContinuousFailedCount++;
                item.LastErrorMessage = ex.Message.Length > 1024 
                    ? ex.Message[..1024] 
                    : ex.Message;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private string ComputeHash(List<string> songIds)
    {
        var sortedIds = songIds.OrderBy(id => id).ToList();
        var concatenated = string.Join(",", sortedIds);
        var bytes = Encoding.UTF8.GetBytes(concatenated);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}