using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public class SoundalikeMatchService(AcousticFingerprintService fingerprintService) : ISoundalikeMatchService
{
    public async Task<SoundalikeMatchResult> MatchAsync(MusicDbContext db, long ownerId, IReadOnlyList<long> songIds, CancellationToken cancellationToken = default)
    {
        var ids = songIds.Distinct().ToList();

        var songsById = await db.Songs
            .Where(s => ids.Contains(s.Id))
            .Include(s => s.Album)
            .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
            .Include(s => s.Cover)
            .AsSplitQuery()
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var songs = ids.Where(songsById.ContainsKey).Select(id => songsById[id]).ToList();

        var foreignSong = songs.FirstOrDefault(s => s.OwnerId != ownerId);
        if (foreignSong != null)
            throw new UnauthorizedAccessException($"User {ownerId} does not own song {foreignSong.Id}");

        var pairwiseScores = await fingerprintService.ComputePairwiseScoresAsync(songs, cancellationToken);

        // A score only stands for the whole set when every pair of songs could be compared
        var pairsCount = songs.Count * (songs.Count - 1) / 2;
        double? matchScore = pairsCount > 0 && pairwiseScores.Count == pairsCount
            ? pairwiseScores.Values.Min()
            : null;

        return new SoundalikeMatchResult { Songs = songs, MatchScore = matchScore };
    }
}
