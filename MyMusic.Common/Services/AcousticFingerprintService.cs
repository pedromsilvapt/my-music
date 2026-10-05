using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public class AcousticFingerprintService(
    MusicDbContext db,
    IFpcalcService fpcalc,
    ILogger<AcousticFingerprintService> logger)
{
    public bool IsAvailable() => fpcalc.IsAvailable();

    public const double DefaultFingerprintLength = 15.0;
    public const int DefaultFingerprintAlgorithm = 2;
    private const double DefaultLookupThreshold = 0.25;
    private const double DefaultMatchThreshold = 0.95;

    public async Task<SongAcousticFingerprint?> GetOrCreateFingerprintAsync(
        Song song,
        double lengthSeconds = DefaultFingerprintLength,
        int algorithm = DefaultFingerprintAlgorithm,
        CancellationToken ct = default)
    {
        if (!fpcalc.IsAvailable())
        {
            return null;
        }

        var existing = await db.SongAcousticFingerprints
            .FirstOrDefaultAsync(f => 
                f.Checksum == song.Checksum && 
                f.ChecksumAlgorithm == song.ChecksumAlgorithm &&
                f.OwnerId == song.OwnerId, ct);

        if (existing != null && 
            Math.Abs(existing.FingerprintLength - lengthSeconds) < 0.001 &&
            existing.FingerprintAlgorithm == algorithm)
        {
            return existing;
        }

        var result = await fpcalc.FingerprintAsync(song.RepositoryPath, lengthSeconds, algorithm, ct);
        if (result == null)
        {
            return null;
        }

        var fingerprint = existing ?? new SongAcousticFingerprint
        {
            Checksum = song.Checksum,
            ChecksumAlgorithm = song.ChecksumAlgorithm,
            OwnerId = song.OwnerId,
            CreatedAt = DateTime.UtcNow
        };

        fingerprint.Fingerprint = FingerprintEncoding.ToBytes(result.Fingerprint);
        fingerprint.Duration = result.Duration;
        fingerprint.FingerprintLength = lengthSeconds;
        fingerprint.FingerprintAlgorithm = algorithm;
        fingerprint.ModifiedAt = DateTime.UtcNow;

        if (existing == null)
        {
            db.SongAcousticFingerprints.Add(fingerprint);
        }
        else
        {
            db.SongAcousticFingerprints.Update(fingerprint);
        }

        await db.SaveChangesAsync(ct);
        return fingerprint;
    }

    public async Task<List<List<Song>>> FindDuplicatesAsync(
        long ownerId,
        double lookupThreshold = DefaultLookupThreshold,
        double matchThreshold = DefaultMatchThreshold,
        CancellationToken ct = default)
    {
        logger.LogDebug("FindDuplicatesAsync called for owner {OwnerId}, fpcalc available: {IsAvailable}", ownerId, fpcalc.IsAvailable());
        
        var songs = await db.Songs
            .Where(s => s.OwnerId == ownerId)
            .ToListAsync(ct);

        logger.LogDebug("Found {SongCount} songs for owner {OwnerId}", songs.Count, ownerId);

        var excludedPairs = await db.ExcludedDuplicatePairs
            .Where(p => p.OwnerId == ownerId)
            .Select(p => new { p.SongAId, p.SongBId })
            .ToListAsync(ct);

        var excludedSet = new HashSet<(long, long)>();
        foreach (var pair in excludedPairs)
        {
            var key = pair.SongAId < pair.SongBId 
                ? (pair.SongAId, pair.SongBId) 
                : (pair.SongBId, pair.SongAId);
            excludedSet.Add(key);
        }

        var fingerprints = new Dictionary<long, uint[]>();
        foreach (var song in songs)
        {
            var fp = await GetOrCreateFingerprintAsync(song, ct: ct);
            if (fp != null)
            {
                fingerprints[song.Id] = FingerprintEncoding.FromBytes(fp.Fingerprint);
                logger.LogDebug("Generated fingerprint for song {SongId}, length: {Length}", song.Id, fingerprints[song.Id].Length);
            }
            else
            {
                logger.LogDebug("Failed to generate fingerprint for song {SongId}", song.Id);
            }
        }

        logger.LogDebug("Generated {Count} fingerprints out of {Total} songs", fingerprints.Count, songs.Count);

        if (fingerprints.Count == 0)
        {
            logger.LogWarning("No fingerprints generated, returning empty list");
            return [];
        }

        var index = new FingerprintIndex<long>();
        foreach (var (songId, fprint) in fingerprints)
        {
            index.Add(songId, fprint);
        }

        var edges = new ConcurrentDictionary<long, List<long>>();

        var thresh = (int)(index.AverageLength * lookupThreshold);

        foreach (var (songIdA, fprintA) in fingerprints)
        {
            var candidates = index.FindCandidates(fprintA, thresh, id => id == songIdA);

            foreach (var songIdB in candidates)
            {
                if (!fingerprints.TryGetValue(songIdB, out var fprintB))
                    continue;

                var key = songIdA < songIdB ? (songIdA, songIdB) : (songIdB, songIdA);
                if (excludedSet.Contains(key))
                    continue;

                var (score, _, _) = CompareFingerprints(fprintA, fprintB, false);
                if (score >= matchThreshold)
                {
                    edges.AddOrUpdate(songIdA, [songIdB], (_, list) =>
                    {
                        list.Add(songIdB);
                        return list;
                    });
                    edges.AddOrUpdate(songIdB, [songIdA], (_, list) =>
                    {
                        list.Add(songIdA);
                        return list;
                    });
                }
            }
        }

        var components = FindConnectedComponents(edges);
        var result = new List<List<Song>>();

        foreach (var component in components)
        {
            var groupSongs = songs.Where(s => component.Contains(s.Id)).ToList();
            if (groupSongs.Count >= 2)
            {
                result.Add(groupSongs);
            }
        }

        return result;
    }

    public async Task ExcludePairAsync(
        long songAId, 
        long songBId, 
        long ownerId, 
        string? reason = null, 
        CancellationToken ct = default)
    {
        var (aId, bId) = songAId < songBId ? (songAId, songBId) : (songBId, songAId);

        var existing = await db.ExcludedDuplicatePairs
            .FirstOrDefaultAsync(p => 
                p.SongAId == aId && 
                p.SongBId == bId && 
                p.OwnerId == ownerId, ct);

        if (existing != null)
        {
            return;
        }

        db.ExcludedDuplicatePairs.Add(new ExcludedDuplicatePair
        {
            SongAId = aId,
            SongBId = bId,
            OwnerId = ownerId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsExcludedPairAsync(
        long songAId, 
        long songBId, 
        long ownerId, 
        CancellationToken ct = default)
    {
        var (aId, bId) = songAId < songBId ? (songAId, songBId) : (songBId, songAId);
        return await db.ExcludedDuplicatePairs
            .AnyAsync(p => 
                p.SongAId == aId && 
                p.SongBId == bId && 
                p.OwnerId == ownerId, ct);
    }

    public async Task<List<ExcludedDuplicatePair>> GetExcludedPairsAsync(
        long ownerId, 
        CancellationToken ct = default)
    {
        return await db.ExcludedDuplicatePairs
            .Include(p => p.SongA)
            .Include(p => p.SongB)
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public (double Score, int OffsetA, int OffsetB) CompareFingerprints(
        uint[] a, 
        uint[] b, 
        bool minLength) => FingerprintIndex<long>.Compare(a, b, minLength);

    private static List<HashSet<long>> FindConnectedComponents(ConcurrentDictionary<long, List<long>> edges)
    {
        var visited = new HashSet<long>();
        var components = new List<HashSet<long>>();

        HashSet<long> Dfs(long start)
        {
            var component = new HashSet<long>();
            var stack = new Stack<long>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visited.Add(current))
                    continue;

                component.Add(current);

                if (edges.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }
            }

            return component;
        }

        foreach (var (node, _) in edges)
        {
            if (!visited.Contains(node))
            {
                components.Add(Dfs(node));
            }
        }

        return components;
    }

    public async IAsyncEnumerable<(List<Song> Group, Dictionary<string, double> PairwiseScores)> FindDuplicatesWithScoresAsync(
        long ownerId,
        double lookupThreshold = DefaultLookupThreshold,
        double matchThreshold = DefaultMatchThreshold,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var groups = await FindDuplicatesAsync(ownerId, lookupThreshold, matchThreshold, ct);

        foreach (var group in groups)
        {
            var pairwiseScores = await ComputePairwiseScoresAsync(group, ct);

            yield return (group, pairwiseScores);
        }
    }

    /// <summary>
    /// Compares every pair of the given songs by their acoustic fingerprints. The scores are keyed by
    /// <c>"{lowerSongId}-{higherSongId}"</c>; pairs with a song that cannot be fingerprinted are left out.
    /// </summary>
    public async Task<Dictionary<string, double>> ComputePairwiseScoresAsync(
        IReadOnlyList<Song> group,
        CancellationToken ct = default)
    {
        var pairwiseScores = new Dictionary<string, double>();
        var fingerprints = new Dictionary<long, uint[]>();

        foreach (var song in group)
        {
            var fp = await GetOrCreateFingerprintAsync(song, ct: ct);
            if (fp != null)
            {
                fingerprints[song.Id] = FingerprintEncoding.FromBytes(fp.Fingerprint);
            }
        }

        for (var i = 0; i < group.Count; i++)
        {
            for (var j = i + 1; j < group.Count; j++)
            {
                var songA = group[i];
                var songB = group[j];

                if (fingerprints.TryGetValue(songA.Id, out var fpA) &&
                    fingerprints.TryGetValue(songB.Id, out var fpB))
                {
                    var (score, _, _) = CompareFingerprints(fpA, fpB, false);
                    var key = $"{Math.Min(songA.Id, songB.Id)}-{Math.Max(songA.Id, songB.Id)}";
                    pairwiseScores[key] = score;
                }
            }
        }

        return pairwiseScores;
    }
}
