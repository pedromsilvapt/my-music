namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Service for merging artists by hand: one artist is kept, and takes in the songs and albums of the others.
/// </summary>
public interface IArtistMergeService
{
    /// <summary>
    ///     Merges some of <paramref name="ownerId"/>'s artists into another one, as a single operation. Every song a
    ///     <paramref name="sourceIds"/> artist performs gets the <paramref name="targetId"/> artist in its place, and
    ///     every album of theirs becomes an album of the target: an album whose name the target already has (or that
    ///     two of them share) is merged into that one. Every affected song has its file, checksum, devices, label
    ///     and path updated. The target takes the sources' links to external sources, and their photo and background
    ///     when it has none; the sources are then deleted. All or nothing: a failing song leaves every artist, every
    ///     album, every song and every file as they were.
    /// </summary>
    /// <exception cref="ArtistNotFoundException">The owner does not have one of the artists.</exception>
    /// <exception cref="ValidationException">
    ///     There is no artist to merge, the target is also one of the artists to merge, or one of the artists to
    ///     merge is a placeholder artist.
    /// </exception>
    Task MergeAsync(long ownerId, long targetId, IReadOnlyCollection<long> sourceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     What <see cref="MergeAsync"/> would do to the songs and albums, without changing anything. Throws the
    ///     same exceptions.
    /// </summary>
    Task<ArtistMergePreview> PreviewAsync(long ownerId, long targetId, IReadOnlyCollection<long> sourceIds,
        CancellationToken cancellationToken = default);
}

/// <param name="SongsCount">
///     The songs the merge changes: the ones a merged artist performs, together with the ones of their albums.
/// </param>
/// <param name="MergedAlbumsCount">
///     The albums of the merged artists that are merged into an album with the same name, instead of just becoming
///     albums of the target.
/// </param>
public record ArtistMergePreview(int SongsCount, int MergedAlbumsCount);
