using Refit;

namespace MyMusic.Common.Sources;

public interface ISource
{
    // /// <summary>
    // /// Given a URL, try  to extrapolate the most likely search string that would give a return for the music associated with it
    // /// </summary>
    // /// <param name="url"></param>
    // /// <returns></returns>
    // public Task<string> ReverseSearch(string url);

    [Get("/songs/search/{query}")]
    public Task<List<SourceSong>> SearchSongsAsync(string query, CancellationToken cancellationToken = default);

    [Get("/songs/{id}")]
    public Task<SourceSong> GetSongAsync(string id, CancellationToken cancellationToken = default);

    [Get("/songs/purchase/{id}")]
    public Task<Stream> PurchaseSongAsync(string id, CancellationToken cancellationToken = default);

    [Get("/albums/search/{query}")]
    public Task<List<SourceAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default);
}