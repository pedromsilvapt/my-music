using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Filters;
using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

/// <summary>
/// Implementation of purchase search service that centralizes search logic
/// with fuzzy matching and filter DSL support.
/// </summary>
public class PurchasesSearchService : IPurchasesSearchService
{
    private readonly ISourcesService _sourcesService;
    private readonly ILogger<PurchasesSearchService> _logger;
    private readonly int _maxResultsToHash;

    public PurchasesSearchService(
        ISourcesService sourcesService,
        IOptions<Config> config,
        ILogger<PurchasesSearchService> logger)
    {
        _sourcesService = sourcesService;
        _logger = logger;
        _maxResultsToHash = config.Value.WishlistMaxResultsToHash;
    }

    /// <inheritdoc />
    public async Task<List<SourceSong>> SearchAsync(
        long sourceId,
        string query,
        string? filter = null,
        bool fuzzyMatch = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Starting purchase search - SourceId: {SourceId}, Query: {Query}, Filter: {Filter}, FuzzyMatch: {FuzzyMatch}",
            sourceId, query, filter ?? "(none)", fuzzyMatch);

        // Get raw results from source
        var source = await _sourcesService.GetSourceClientAsync(sourceId, cancellationToken);
        var results = await source.SearchSongsAsync(query, cancellationToken);

        var rawCount = results.Count;
        _logger.LogDebug(
            "Source returned {RawCount} raw results for query '{Query}' from source {SourceId}",
            rawCount, query, sourceId);

        // Apply fuzzy matching if enabled
        if (fuzzyMatch)
        {
            results = InMemoryFilterBuilder.ApplyFuzzySearch(
                results,
                query,
                s => s.SearchableText).ToList();

            var fuzzyCount = results.Count;
            _logger.LogDebug(
                "After fuzzy matching: {FuzzyCount} results (filtered {Filtered} songs)",
                fuzzyCount, rawCount - fuzzyCount);
        }
        else
        {
            _logger.LogDebug("Fuzzy matching skipped (fuzzyMatch=false)");
        }

        // Apply filter DSL if provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            if (filterRequest.Rules.Count > 0)
            {
                var preFilterCount = results.Count;
                results = InMemoryFilterBuilder.ApplyFilter(results, filterRequest).ToList();
                var postFilterCount = results.Count;

                _logger.LogDebug(
                    "After filter DSL '{Filter}': {PostFilterCount} results (filtered {Filtered} songs)",
                    filter, postFilterCount, preFilterCount - postFilterCount);
            }
            else
            {
                _logger.LogDebug("Filter DSL parsed but no rules to apply");
            }
        }

        var finalCount = results.Count;
        _logger.LogInformation(
            "Purchase search completed - SourceId: {SourceId}, Query: '{Query}', Filter: {Filter}, FuzzyMatch: {FuzzyMatch}, Raw: {RawCount}, Final: {FinalCount}",
            sourceId, query, filter ?? "(none)", fuzzyMatch, rawCount, finalCount);

        return results;
    }

    /// <summary>
    /// Searches for songs and returns song IDs for hash computation.
    /// This is a convenience method used by wishlist services.
    /// </summary>
    public async Task<List<string>> SearchForHashAsync(
        long sourceId,
        string query,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(sourceId, query, filter, fuzzyMatch: true, cancellationToken);

        return results
            .Take(_maxResultsToHash)
            .Select(s => s.Id)
            .ToList();
    }
}
