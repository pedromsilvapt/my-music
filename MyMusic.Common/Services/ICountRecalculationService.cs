namespace MyMusic.Common.Services;

/// <summary>
/// Service for recalculating denormalized count fields on albums and artists.
/// </summary>
public interface ICountRecalculationService
{
    /// <summary>
    /// Recalculates <see cref="Entities.Album.SongsCount"/> for all albums
    /// by counting their associated songs.
    /// </summary>
    Task<int> RecalculateAlbumSongCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates <see cref="Entities.Artist.SongsCount"/> for all artists
    /// by counting their associated song artists.
    /// </summary>
    Task<int> RecalculateArtistSongCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates <see cref="Entities.Artist.AlbumsCount"/> for all artists
    /// by counting their associated albums.
    /// </summary>
    Task<int> RecalculateArtistAlbumCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates all denormalized counts (album song counts, artist song counts,
    /// and artist album counts) for all users.
    /// </summary>
    Task<RecalculateCountsResult> RecalculateAllCountsAsync(CancellationToken cancellationToken = default);
}

public record RecalculateCountsResult
{
    public required int AlbumsUpdated { get; set; }
    public required int ArtistsSongsUpdated { get; set; }
    public required int ArtistsAlbumsUpdated { get; set; }
}