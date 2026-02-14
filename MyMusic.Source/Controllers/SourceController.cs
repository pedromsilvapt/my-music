using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Sources;

namespace MyMusic.Source.Controllers;

// NOTE Mark the controller as abstract to prevent it from being included by default
[Controller]
public abstract class SourceController(ILogger<SourceController> logger, ISource source) : ControllerBase, ISource
{
    private readonly ILogger<SourceController> _logger = logger;

    #region Songs

    [HttpGet("/songs/search/{query}", Name = "Search Songs")]
    public async Task<List<SourceSong>> SearchSongsAsync(string query, CancellationToken cancellationToken = default)
    {
        return await source.SearchSongsAsync(query, cancellationToken);
    }

    [HttpGet("/songs/{id}", Name = "Get Song")]
    public async Task<SourceSong> GetSongAsync(string id, CancellationToken cancellationToken = default)
    {
        return await source.GetSongAsync(id, cancellationToken);
    }

    [HttpGet("/songs/purchase/{id}", Name = "Purchase Song")]
    public async Task<Stream> PurchaseSongAsync(string id, CancellationToken cancellationToken = default)
    {
        return await source.PurchaseSongAsync(id, cancellationToken);
    }

    #endregion Songs

    #region Albums

    [HttpGet("/albums/search/{query}", Name = "Search Albums")]
    public async Task<List<SourceAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default)
    {
        return await source.SearchAlbumsAsync(query, cancellationToken);
    }

    #endregion Albums
}