using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Computes a field-by-field metadata diff between a selected song history
/// revision and its immediate predecessor, for display in the version diff
/// viewer. Ownership is verified via the song's <c>OwnerUserId</c> against
/// <see cref="ICurrentUser"/>.
/// </summary>
public interface ISongHistoryVersionDiffService
{
    /// <summary>
    /// Returns the metadata diff for <paramref name="historyId"/> compared to
    /// the previous revision of the same song. Returns <c>null</c> when the
    /// history entry or its song cannot be found, or when the song does not
    /// belong to the current user.
    /// </summary>
    /// <param name="songId">The song whose history is being queried (route parameter).</param>
    /// <param name="historyId">The specific history entry to diff against its predecessor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="SongHistoryVersionDiffResult"/> containing the normalized
    /// diff and revision metadata, or <c>null</c> if not found / not owned.
    /// </returns>
    Task<SongHistoryVersionDiffResult?> GetVersionDiffAsync(
        long songId,
        long historyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a version diff computation: the normalized metadata diff plus
/// the revision numbers and timestamps of the two compared snapshots.
/// </summary>
public record SongHistoryVersionDiffResult
{
    /// <summary>The field-by-field metadata diff.</summary>
    public required SongHistoryVersionDiffModel Metadata { get; init; }

    /// <summary>The timestamp of the older (previous) snapshot, or <c>null</c> when the selected entry is the first revision.</summary>
    public DateTime? OldVersionDate { get; init; }

    /// <summary>The timestamp of the selected (newer) snapshot.</summary>
    public required DateTime NewVersionDate { get; init; }

    /// <summary>The revision number of the older snapshot, or <c>null</c> when the selected entry is the first revision.</summary>
    public int? OldRevision { get; init; }

    /// <summary>The revision number of the selected snapshot.</summary>
    public required int NewRevision { get; init; }
}