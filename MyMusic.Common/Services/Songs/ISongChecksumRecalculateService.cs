namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Brings a song's stored checksum back in line with its file in the music repository.
/// </summary>
public interface ISongChecksumRecalculateService
{
    /// <summary>
    /// Calculates the checksum of the song's file and stores it when it differs from the recorded one. The file is
    /// only read: its modification time and the song's devices are left untouched.
    /// </summary>
    /// <param name="songId">The ID of the song, which must belong to the current user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SongChecksumRecalculateResult> RecalculateAsync(long songId, CancellationToken cancellationToken = default);
}

public record SongChecksumRecalculateResult
{
    /// <summary>Whether the file's checksum differed from the recorded one, which was then updated.</summary>
    public required bool Changed { get; init; }
}
