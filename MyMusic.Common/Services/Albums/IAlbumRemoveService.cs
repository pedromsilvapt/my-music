namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Service for deleting albums by hand, moving the songs they still have to the placeholder album.
/// </summary>
public interface IAlbumRemoveService
{
    /// <summary>
    ///     Deletes some of <paramref name="ownerId"/>'s albums, as a single operation. Their songs move to the
    ///     placeholder album of the same album artist, with their files, checksums, devices, labels and paths
    ///     updated. All or nothing: a failing song leaves every album, every song and every file as they were.
    /// </summary>
    /// <exception cref="AlbumNotFoundException">The owner does not have one of the albums.</exception>
    /// <exception cref="ValidationException">
    ///     One of the albums is a placeholder album that still has songs, or that the songs of the others move to.
    /// </exception>
    Task RemoveAsync(long ownerId, IReadOnlyCollection<long> albumIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when the user has no album with the requested id.
/// </summary>
public class AlbumNotFoundException(long albumId) : Exception($"Album not found with id {albumId}");
