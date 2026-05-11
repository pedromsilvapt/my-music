namespace MyMusic.Common.Services;

/// <summary>
/// Service for deleting genres.
/// </summary>
public interface IGenreDeleteService
{
    /// <summary>
    /// Deletes genres by their IDs.
    /// </summary>
    /// <param name="genreIds">The IDs of the genres to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(long[] genreIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes genres that are no longer referenced by any songs.
    /// </summary>
    /// <param name="genreIds">The IDs of genres to check for deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of deleted genres.</returns>
    Task<long[]> DeleteIfUnusedAsync(long[] genreIds, CancellationToken cancellationToken = default);
}
