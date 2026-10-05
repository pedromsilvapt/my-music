using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public interface ISoundalikeMatchService
{
    /// <summary>
    /// Scores how alike the given songs sound, whatever the score: no threshold is applied, since the songs were
    /// picked by the user rather than detected.
    /// </summary>
    Task<SoundalikeMatchResult> MatchAsync(MusicDbContext db, long ownerId, IReadOnlyList<long> songIds, CancellationToken cancellationToken = default);
}

public record SoundalikeMatchResult
{
    /// <summary>The songs found, in the order they were asked for.</summary>
    public required List<Song> Songs { get; init; }

    /// <summary>
    /// The lowest score between any two of the songs, or null when not every pair could be compared (fpcalc is not
    /// available, or a song could not be fingerprinted).
    /// </summary>
    public required double? MatchScore { get; init; }
}
