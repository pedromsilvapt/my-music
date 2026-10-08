using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Sets the timestamps of a song to values chosen by the user.
/// </summary>
public interface ISongTimestampsUpdateService
{
    /// <summary>
    /// Stores the given timestamps on the song exactly as given: <see cref="Song.ModifiedAt"/> is not bumped to
    /// the current time. Nothing else on the song changes, and its devices are left untouched.
    /// </summary>
    /// <param name="songId">The ID of the song, which must belong to the current user.</param>
    /// <param name="timestamps">The timestamps to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated song.</returns>
    Task<Song> UpdateAsync(long songId, SongTimestampsUpdate timestamps,
        CancellationToken cancellationToken = default);
}

public record SongTimestampsUpdate
{
    /// <summary>When the song was first created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the song last changed.</summary>
    public required DateTime ModifiedAt { get; init; }

    /// <summary>When the song was added to the database; null clears it.</summary>
    public required DateTime? AddedAt { get; init; }

    /// <summary>When the song's file last changed; null clears it.</summary>
    public required DateTime? FileModifiedAt { get; init; }
}
