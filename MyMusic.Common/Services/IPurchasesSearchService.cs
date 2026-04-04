using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

/// <summary>
/// Service for searching songs from purchase sources with optional fuzzy matching and filter DSL support.
/// </summary>
public interface IPurchasesSearchService
{
    /// <summary>
    /// Searches for songs from a source with optional fuzzy matching and filter DSL application.
    /// </summary>
    /// <param name="sourceId">The ID of the source to search</param>
    /// <param name="query">The search query</param>
    /// <param name="filter">Optional filter DSL expression</param>
    /// <param name="fuzzyMatch">Whether to apply fuzzy matching (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of source songs matching the search criteria</returns>
    Task<List<SourceSong>> SearchAsync(
        long sourceId,
        string query,
        string? filter = null,
        bool fuzzyMatch = true,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Searches for songs and returns song IDs for hash computation.
    /// This is a convenience method used by wishlist services.
    /// </summary>
    Task<List<string>> SearchForHashAsync(
        long sourceId,
        string query,
        string? filter = null,
        CancellationToken cancellationToken = default);
}
