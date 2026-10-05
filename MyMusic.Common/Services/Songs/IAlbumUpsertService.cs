using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Service for finding or creating the album an album artist has with a given name.
/// </summary>
public interface IAlbumUpsertService
{
    /// <summary>
    ///     Returns the album named <paramref name="name"/> of <paramref name="artist"/>, creating it (without saving)
    ///     when the artist has none. Albums of the same name by other artists are never matched.
    /// </summary>
    /// <remarks>
    ///     The caller must hold the <see cref="AdvisoryLockScope.Album"/> lock of the album, and the
    ///     <see cref="AdvisoryLockScope.Artist"/> lock of its artist, so concurrent requests cannot create it twice.
    /// </remarks>
    /// <param name="artist">Tracked by <paramref name="db"/>; may not be saved yet.</param>
    Task<Album> UpsertAsync(MusicDbContext db, long ownerId, string name, Artist artist,
        CancellationToken cancellationToken = default);
}
