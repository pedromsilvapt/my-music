namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Service for editing artists by hand: renaming them.
/// </summary>
public interface IArtistEditService
{
    /// <summary>
    ///     Edits some of <paramref name="ownerId"/>'s artists, as a single operation. Names are trimmed. A renamed
    ///     artist keeps its row, and every song it performs or that is in one of its albums has its file, checksum,
    ///     devices, label and path updated; an artist whose name stays the same is left alone. Artist names are not
    ///     unique, so an artist can take the name another one has: both remain. All or nothing: a failing song
    ///     leaves every artist, every song and every file as they were.
    /// </summary>
    /// <exception cref="ArtistNotFoundException">The owner does not have one of the artists.</exception>
    /// <exception cref="ValidationException">
    ///     A name is empty or too long, an artist is edited more than once, or a placeholder artist is renamed.
    /// </exception>
    Task EditAsync(long ownerId, IReadOnlyCollection<ArtistEditInput> edits,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The new state of one artist in an artist edit operation. Mirrors an item of <c>UpdateArtistsRequest</c> but lives
/// in <see cref="MyMusic.Common"/> so the service has no dependency on the Server DTO layer.
/// </summary>
public record ArtistEditInput
{
    public required long ArtistId { get; init; }
    public required string Name { get; init; }
}
