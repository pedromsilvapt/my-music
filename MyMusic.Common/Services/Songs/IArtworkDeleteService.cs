namespace MyMusic.Common.Services;

/// <summary>
/// Service for deleting artworks.
/// </summary>
public interface IArtworkDeleteService
{
    /// <summary>
    /// Deletes artworks by their IDs.
    /// </summary>
    /// <param name="artworkIds">The IDs of the artworks to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(long[] artworkIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes artworks that are no longer referenced by any songs, albums, or artists.
    /// </summary>
    /// <param name="artworkIds">The IDs of artworks to check for deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of deleted artworks.</returns>
    Task<long[]> DeleteIfUnusedAsync(long[] artworkIds, CancellationToken cancellationToken = default);
}
