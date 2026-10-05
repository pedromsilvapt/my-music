namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Service for deleting artists by hand, along with their albums, taking them out of every song.
/// </summary>
public interface IArtistRemoveService
{
    /// <summary>
    ///     Deletes some of <paramref name="ownerId"/>'s artists and all of their albums, as a single operation. Every
    ///     song they perform loses them (getting the placeholder artist when no other remains), and every song of
    ///     their albums moves to the placeholder album of its first remaining artist. Their files, checksums,
    ///     devices, labels and paths are updated. All or nothing: a failing song leaves every artist, every album,
    ///     every song and every file as they were.
    /// </summary>
    /// <exception cref="ArtistNotFoundException">The owner does not have one of the artists.</exception>
    /// <exception cref="ValidationException">
    ///     One of the artists is a placeholder artist that still has songs, or that the songs of the others move to.
    /// </exception>
    Task RemoveAsync(long ownerId, IReadOnlyCollection<long> artistIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when the user has no artist with the requested id.
/// </summary>
public class ArtistNotFoundException(long artistId) : Exception($"Artist not found with id {artistId}");
