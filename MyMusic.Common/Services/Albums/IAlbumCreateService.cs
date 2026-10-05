using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Service for creating an empty album, by hand rather than as a side effect of a song import or edit.
/// </summary>
public interface IAlbumCreateService
{
    /// <summary>
    ///     Creates an album of one of <paramref name="ownerId"/>'s artists. The name is trimmed.
    /// </summary>
    /// <exception cref="AlbumAlreadyExistsException">The artist already has an album with that name.</exception>
    /// <exception cref="ValidationException">The name is empty or too long, or the artist does not exist.</exception>
    Task<Album> CreateAsync(long ownerId, AlbumCreateInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Input for an album create operation. Mirrors <c>CreateAlbumRequest</c> but lives in
/// <see cref="MyMusic.Common"/> so the service has no dependency on the Server DTO layer.
/// </summary>
public record AlbumCreateInput
{
    public required string Name { get; init; }
    public required long ArtistId { get; init; }
    public int? Year { get; init; }
}

/// <summary>
/// Thrown when an artist already has an album with the requested name: album names are unique per artist.
/// </summary>
public class AlbumAlreadyExistsException(string message) : ValidationException(message);
