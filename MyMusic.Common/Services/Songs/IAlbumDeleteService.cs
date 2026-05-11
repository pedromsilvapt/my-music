namespace MyMusic.Common.Services;

/// <summary>
/// Service for deleting albums.
/// </summary>
public interface IAlbumDeleteService
{
    /// <summary>
    /// Deletes albums by their IDs.
    /// </summary>
    /// <param name="albumIds">The IDs of the albums to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(long[] albumIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes albums that are no longer referenced by any songs.
    /// </summary>
    /// <param name="albumIds">The IDs of albums to check for deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of deleted albums.</returns>
    Task<long[]> DeleteIfUnusedAsync(long[] albumIds, CancellationToken cancellationToken = default);
}
