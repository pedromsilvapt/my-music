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
    IPurchasesSearchService purchasesSearchService,
    IOptions<Config> config,
    ILogger<WishlistService> logger) : IWishlistService
{
    public async Task<WishlistItem> CreateAsync(long userId, long sourceId, string query, string? filter,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.WishlistItems
            .Include(w => w.Source)
            .FirstOrDefaultAsync(w => w.OwnerId == userId && w.SourceId == sourceId && w.Query == query && w.Filter == filter,
                cancellationToken);

        if (existing != null)
        {
            logger.LogInformation(
                "Wishlist item already exists - UserId: {UserId}, SourceId: {SourceId}, Query: {Query}, Filter: {Filter}, ItemId: {ItemId}",
                userId, sourceId, query, filter ?? "(none)", existing.Id);
            return existing;
        }

        var user = await db.Users.FindAsync([userId], cancellationToken) ??
                   throw new Exception($"User not found with id {userId}");

        // Compute initial hash by searching the source directly
        // This ensures the hash reflects actual current results, not client-provided data
        List<string> songIds;
        try
        {
            songIds = await purchasesSearchService.SearchForHashAsync(sourceId, query, filter, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to compute initial hash for wishlist item - UserId: {UserId}, SourceId: {SourceId}, Query: {Query}, Filter: {Filter}",
                userId, sourceId, query, filter ?? "(none)");
            throw new Exception($"Failed to compute wishlist hash: {ex.Message}", ex);
        }

        // Handle null/empty songIds gracefully (empty search results are valid)
        songIds ??= [];
        var sortedIds = songIds.OrderBy(id => id).ToList();
        var concatenated = string.Join(",", sortedIds);
        var bytes = Encoding.UTF8.GetBytes(concatenated);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        
        logger.LogInformation(
            "Creating wishlist item - UserId: {UserId}, SourceId: {SourceId}, Query: {Query}, Filter: {Filter}, Count: {Count}, Hash: {Hash}",
            userId, sourceId, query, filter ?? "(none)", songIds.Count, hash);
        
        var now = DateTime.UtcNow;

        var item = new WishlistItem
        {
            Owner = user,
            OwnerId = userId,
            SourceId = sourceId,
            Query = query,
            Filter = filter,
            Hash = hash,
            Status = WishlistItemStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WishlistItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Wishlist item created - ItemId: {ItemId}, UserId: {UserId}, SourceId: {SourceId}, Query: {Query}, Filter: {Filter}",
            item.Id, userId, sourceId, query, filter ?? "(none)");

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

        logger.LogInformation(
            "Updating hash for wishlist item {ItemId} - Query: {Query}, Filter: {Filter}, SourceId: {SourceId}, SourceName: {SourceName}, OldHash: {OldHash}",
            item.Id, item.Query, item.Filter ?? "(none)", item.SourceId, item.Source?.Name ?? "Unknown", item.Hash);

        // Use PurchasesSearchService with fuzzy matching and the item's filter
        var songIds = await purchasesSearchService.SearchForHashAsync(
            item.SourceId,
            item.Query,
            item.Filter,
            cancellationToken);

        item.Hash = ComputeHash(songIds, item.Id, item.Query, item.Filter, "KeepAction");
        item.Status = WishlistItemStatus.Active;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Wishlist item {ItemId} hash updated - Query: {Query}, Filter: {Filter}, SongsFound: {SongCount}, NewHash: {NewHash}",
            item.Id, item.Query, item.Filter ?? "(none)", songIds.Count, item.Hash);

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

        logger.LogInformation("Checking for wishlist updates - Found {ItemCount} active items to check", activeItems.Count);

        foreach (var item in activeItems)
        {
            try
            {
                logger.LogInformation(
                    "Checking wishlist item {ItemId} - Query: {Query}, Filter: {Filter}, SourceId: {SourceId}, SourceName: {SourceName}, CurrentHash: {CurrentHash}",
                    item.Id, item.Query, item.Filter ?? "(none)", item.SourceId, item.Source?.Name ?? "Unknown", item.Hash);

                // Use PurchasesSearchService with fuzzy matching and the item's filter
                var songIds = await purchasesSearchService.SearchForHashAsync(
                    item.SourceId,
                    item.Query,
                    item.Filter,
                    cancellationToken);

                var newHash = ComputeHash(songIds, item.Id, item.Query, item.Filter, "BackgroundCheck");

                if (newHash != item.Hash)
                {
                    logger.LogInformation(
                        "Wishlist item {ItemId} marked as UPDATED - Query: {Query}, Filter: {Filter}, OldHash: {OldHash}, NewHash: {NewHash}",
                        item.Id, item.Query, item.Filter ?? "(none)", item.Hash, newHash);
                    item.Status = WishlistItemStatus.Updated;
                }
                else
                {
                    logger.LogDebug("Wishlist item {ItemId} hash unchanged - keeping Active status", item.Id);
                }

                item.UpdatedAt = DateTime.UtcNow;
                item.ContinuousFailedCount = 0;
                item.LastErrorMessage = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check updates for wishlist item {ItemId} (Query: {Query}, Filter: {Filter})",
                    item.Id, item.Query, item.Filter ?? "(none)");
                item.ContinuousFailedCount++;
                item.LastErrorMessage = ex.Message.Length > 1024 
                    ? ex.Message[..1024] 
                    : ex.Message;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private string ComputeHash(List<string> songIds, long itemId, string query, string? filter, string context)
    {
        var sortedIds = songIds.OrderBy(id => id).ToList();
        var concatenated = string.Join(",", sortedIds);
        var bytes = Encoding.UTF8.GetBytes(concatenated);
        var hash = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hash);

        // Format the IDs for logging (first 10, with ... if more)
        var idsForLogging = songIds.Count switch
        {
            0 => "[]",
            <= 10 => $"[{string.Join(", ", songIds)}]",
            _ => $"[{string.Join(", ", songIds.Take(10))}, ... ({songIds.Count - 10} more)]"
        };

        logger.LogInformation(
            "Computing hash for wishlist item {ItemId} - Context: {Context}, Query: {Query}, Filter: {Filter}, Count: {Count}, IDs: {Ids}, ResultHash: {Hash}",
            itemId, context, query, filter ?? "(none)", songIds.Count, idsForLogging, hashString);

        return hashString;
    }
}