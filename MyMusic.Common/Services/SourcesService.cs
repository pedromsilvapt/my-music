using MyMusic.Common.Sources;
using Refit;

namespace MyMusic.Common.Services;

public class SourcesService(MusicDbContext db) : ISourcesService
{
    public async Task<ISource> GetSourceClientAsync(long id, CancellationToken cancellationToken = default)
    {
        var sourceConfig = await db.Sources.FindAsync([id], cancellationToken);

        if (sourceConfig is null)
        {
            throw new SourceIdNotFoundException(id);
        }

        var source = RestService.For<ISource>(sourceConfig.Address);

        return source;
    }
}

public class SourceIdNotFoundException(long id) : Exception($"Could not find source with id {id}")
{
    public long Id { get; } = id;
}