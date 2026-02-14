using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

public interface ISourcesService
{
    Task<ISource> GetSourceClientAsync(long id, CancellationToken cancellationToken = default);
}