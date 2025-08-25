using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Metadata;

namespace MyMusic.Source.Controllers;

[ApiController]
[Route("songs")]
public class SongsController(ILogger<SongsController> logger, ISource source) : ControllerBase
{
    private readonly ILogger<SongsController> _logger = logger;

    [HttpGet(Name = "Search")]
    [Route("search/{query}")]
    public async Task<IEnumerable<SongMetadata>> Search(string query, CancellationToken cancellationToken = default)
    {
        return await source.SearchSongsAsync(query, cancellationToken);
    }
}