using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Service for creating an artist by hand, rather than as a side effect of a song import or edit.
/// </summary>
public interface IArtistCreateService
{
    /// <summary>
    ///     Creates an artist owned by <paramref name="ownerId"/>. The name is trimmed. Artist names are not unique,
    ///     so an artist is created even when another one has the same name.
    /// </summary>
    /// <exception cref="ValidationException">The name is empty or too long.</exception>
    Task<Artist> CreateAsync(long ownerId, string name, CancellationToken cancellationToken = default);
}
