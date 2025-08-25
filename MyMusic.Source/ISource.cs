using MyMusic.Common.Metadata;

namespace MyMusic.Source;

public interface ISource
{
    /// <summary>
    /// Given an URL, try  to extrapolate the most likely search string that would give a return for the music associated with it
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public Task<string> ReverseSearch(string url);

    public Task<List<AlbumMetadata>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default);

    public Task<List<SongMetadata>> SearchSongsAsync(string query, CancellationToken cancellationToken = default);
        
    public Task<SongMetadata> GetSongAsync(string id, CancellationToken cancellationToken = default);

    public Task<Stream> DownloadAsync(SongMetadata song, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    public string GetSongLink(string songId);

    public string GetAlbumLink(string albumLink);

    public string GetArtistLink(string artistId);
}