using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public interface IWishlistService
{
    Task<WishlistItem> CreateAsync(long userId, long sourceId, string query, string? filter, CancellationToken cancellationToken = default);
    
    Task<List<WishlistItem>> ListAsync(long userId, long? sourceId = null, CancellationToken cancellationToken = default);
    
    Task<WishlistItem> UpdateHashAsync(long id, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    
    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}