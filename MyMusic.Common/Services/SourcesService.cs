using Microsoft.Extensions.Logging;
using MyMusic.Common.Sources;
using Refit;

namespace MyMusic.Common.Services;

public class SourcesService(MusicDbContext db, ILogger<SourcesService> logger) : ISourcesService
{
    private readonly ILogger _logger = logger;

    public async Task<ISource> GetSourceClientAsync(long id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating source client for source {SourceId}", id);
        
        var sourceConfig = await db.Sources.FindAsync([id], cancellationToken);

        if (sourceConfig is null)
        {
            _logger.LogWarning("Source {SourceId} not found in database", id);
            throw new SourceIdNotFoundException(id);
        }

        var source = RestService.For<ISource>(sourceConfig.Address);
        
        _logger.LogDebug("Successfully created source client for {SourceId} at {Address}", id, sourceConfig.Address);

        return source;
    }
}

public class SourceIdNotFoundException(long id) : Exception($"Could not find source with id {id}")
{
    public long Id { get; } = id;
}