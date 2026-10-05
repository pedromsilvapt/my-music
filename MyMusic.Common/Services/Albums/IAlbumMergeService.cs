using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Service for merging albums by hand: one album is kept, and takes in the songs of the others.
/// </summary>
public interface IAlbumMergeService
{
    /// <summary>
    ///     Merges some of <paramref name="ownerId"/>'s albums into another one, as a single operation. Every song of
    ///     the <paramref name="sourceIds"/> albums moves to the <paramref name="targetId"/> album, gaining its album
    ///     artist when the song does not have it yet, with its file, checksum, devices, label and path updated. The
    ///     target takes the sources' links to external sources, and their cover and year when it has none; the
    ///     sources are then deleted. All or nothing: a failing song leaves every album, every song and every file as
    ///     they were.
    /// </summary>
    /// <exception cref="AlbumNotFoundException">The owner does not have one of the albums.</exception>
    /// <exception cref="ValidationException">
    ///     There is no album to merge, the target is also one of the albums to merge, or one of the albums to merge
    ///     is a placeholder album.
    /// </exception>
    Task MergeAsync(long ownerId, long targetId, IReadOnlyCollection<long> sourceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     What <see cref="MergeAsync"/> would do to the songs, without changing anything. Throws the same
    ///     exceptions.
    /// </summary>
    Task<AlbumMergePreview> PreviewAsync(long ownerId, long targetId, IReadOnlyCollection<long> sourceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     The part of a merge that only touches album rows, for an operation that owns the transaction and the
    ///     locks, and has already moved the songs itself (e.g. merging artists, whose same-name albums are merged):
    ///     <paramref name="target"/> takes the links to external sources of <paramref name="sources"/>, and their
    ///     cover and year when it has none; the sources are then deleted, and no longer tracked.
    /// </summary>
    Task MergeRowsAsync(Album target, IReadOnlyList<Album> sources, CancellationToken cancellationToken = default);
}

/// <param name="SongsCount">The songs that move to the target album.</param>
/// <param name="SongsGainingArtistCount">The ones among them that gain the target's album artist.</param>
/// <param name="TargetArtistName">The name of the target's album artist.</param>
public record AlbumMergePreview(int SongsCount, int SongsGainingArtistCount, string TargetArtistName);
