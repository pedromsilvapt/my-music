namespace MyMusic.Common.Services;

/// <summary>
/// Service for deleting artists.
/// </summary>
public interface IArtistDeleteService
{
    /// <summary>
    /// Deletes artists by their IDs.
    /// </summary>
    /// <param name="artistIds">The IDs of the artists to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(long[] artistIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes artists that are no longer referenced by any songs or albums.
    /// </summary>
    /// <param name="artistIds">The IDs of artists to check for deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of deleted artists.</returns>
    Task<long[]> DeleteIfUnusedAsync(long[] artistIds, CancellationToken cancellationToken = default);
}
