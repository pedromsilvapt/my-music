namespace MyMusic.Common.Services;

/// <summary>
/// Service for deleting songs and their associated entities.
/// </summary>
public interface ISongDeleteService
{
    /// <summary>
    /// Deletes one or more songs by their IDs.
    /// </summary>
    /// <param name="songIds">The IDs of the songs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of songs deleted.</returns>
    Task<int> DeleteAsync(long[] songIds, CancellationToken cancellationToken = default);
}
