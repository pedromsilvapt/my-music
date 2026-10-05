namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Service for editing albums by hand: renaming them and changing their year.
/// </summary>
public interface IAlbumEditService
{
    /// <summary>
    ///     Edits some of <paramref name="ownerId"/>'s albums, as a single operation. Names are trimmed. A renamed
    ///     album keeps its row, and each of its songs has its file, checksum, devices, label and path updated; an
    ///     album whose name stays the same is only saved, with no song work. All or nothing: a failing song leaves
    ///     every album, every song and every file as they were.
    /// </summary>
    /// <exception cref="AlbumNotFoundException">The owner does not have one of the albums.</exception>
    /// <exception cref="AlbumAlreadyExistsException">
    ///     A renamed album would share its name with another album of the same artist: they are to be merged instead.
    /// </exception>
    /// <exception cref="ValidationException">
    ///     A name is empty or too long, an album is edited more than once, or a placeholder album is renamed.
    /// </exception>
    Task EditAsync(long ownerId, IReadOnlyCollection<AlbumEditInput> edits,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The new state of one album in an album edit operation. Mirrors an item of <c>UpdateAlbumsRequest</c> but lives in
/// <see cref="MyMusic.Common"/> so the service has no dependency on the Server DTO layer.
/// </summary>
public record AlbumEditInput
{
    public required long AlbumId { get; init; }
    public required string Name { get; init; }
    public int? Year { get; init; }
}
