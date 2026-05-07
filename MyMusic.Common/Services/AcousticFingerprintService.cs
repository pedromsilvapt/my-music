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

    private const double DefaultFingerprintLength = 15.0;
    private const int DefaultFingerprintAlgorithm = 2;
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

        fingerprint.Fingerprint = UintArrayToBytes(result.Fingerprint);
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
                fingerprints[song.Id] = BytesToUintArray(fp.Fingerprint);
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

        var lookup = BuildLookupTable(fingerprints);
        var edges = new ConcurrentDictionary<long, List<long>>();

        var thresh = (int)(fingerprints.Average(f => f.Value.Length) * lookupThreshold);

        foreach (var (songIdA, fprintA) in fingerprints)
        {
            var candidates = FindCandidates(lookup, fprintA, thresh, songIdA);

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
        bool minLength)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return (0, 0, 0);
        }

        int CountBits(uint[] arr1, uint[] arr2, int start1, int start2, int length)
        {
            var count = 0;
            for (var i = 0; i < length; i++)
            {
                if (start1 + i >= arr1.Length || start2 + i >= arr2.Length)
                    break;
                count += 32 - BitCount(arr1[start1 + i] ^ arr2[start2 + i]);
            }
            return count;
        }

        var maxLen = Math.Min(a.Length, b.Length);
        var best = CountBits(a, b, 0, 0, maxLen);
        var aOff = 0;
        var bOff = 0;

        for (var i = 1; i < a.Length; i++)
        {
            var len = Math.Min(a.Length - i, b.Length);
            var cnt = CountBits(a, b, i, 0, len);
            if (cnt > best)
            {
                best = cnt;
                aOff = i;
                bOff = 0;
            }
        }

        for (var i = 1; i < b.Length; i++)
        {
            var len = Math.Min(a.Length, b.Length - i);
            var cnt = CountBits(a, b, 0, i, len);
            if (cnt > best)
            {
                best = cnt;
                aOff = 0;
                bOff = i;
            }
        }

        var total = minLength 
            ? Math.Min(a.Length, b.Length) 
            : Math.Max(a.Length, b.Length);

        return ((double)best / (32 * total), aOff, bOff);
    }

    private static int BitCount(uint x)
    {
        var count = 0;
        while (x != 0)
        {
            count += (int)(x & 1);
            x >>= 1;
        }
        return count;
    }

    private static byte[] UintArrayToBytes(uint[] arr)
    {
        var bytes = new byte[arr.Length * 4];
        for (var i = 0; i < arr.Length; i++)
        {
            bytes[i * 4] = (byte)(arr[i] & 0xFF);
            bytes[i * 4 + 1] = (byte)((arr[i] >> 8) & 0xFF);
            bytes[i * 4 + 2] = (byte)((arr[i] >> 16) & 0xFF);
            bytes[i * 4 + 3] = (byte)((arr[i] >> 24) & 0xFF);
        }
        return bytes;
    }

    private static uint[] BytesToUintArray(byte[] bytes)
    {
        var arr = new uint[bytes.Length / 4];
        for (var i = 0; i < arr.Length; i++)
        {
            arr[i] = (uint)(bytes[i * 4] | (bytes[i * 4 + 1] << 8) | (bytes[i * 4 + 2] << 16) | (bytes[i * 4 + 3] << 24));
        }
        return arr;
    }

    private static Dictionary<ushort, Dictionary<long, short>> BuildLookupTable(Dictionary<long, uint[]> fingerprints)
    {
        var lookup = new Dictionary<ushort, Dictionary<long, short>>();

        foreach (var (songId, fprint) in fingerprints)
        {
            foreach (var v in fprint)
            {
                var key = (ushort)(v >> 16);
                if (!lookup.TryGetValue(key, out var counts))
                {
                    counts = new Dictionary<long, short>();
                    lookup[key] = counts;
                }

                counts[songId] = (short)(counts.GetValueOrDefault(songId) + 1);
            }
        }

        return lookup;
    }

    private static List<long> FindCandidates(
        Dictionary<ushort, Dictionary<long, short>> lookup,
        uint[] fprint,
        int thresh,
        long excludeSongId)
    {
        var hits = new Dictionary<long, Dictionary<ushort, short>>();

        foreach (var v in fprint)
        {
            var key = (ushort)(v >> 16);
            if (!lookup.TryGetValue(key, out var counts))
                continue;

            foreach (var (id, cnt) in counts)
            {
                if (id == excludeSongId)
                    continue;

                if (!hits.TryGetValue(id, out var seen))
                {
                    seen = new Dictionary<ushort, short>();
                    hits[id] = seen;
                }

                var current = seen.GetValueOrDefault(key);
                if (current < cnt)
                {
                    seen[key] = (short)(current + 1);
                }
            }
        }

        var result = new List<long>();
        foreach (var (id, seen) in hits)
        {
            var total = seen.Values.Sum(v => (int)v);
            if (total >= thresh)
            {
                result.Add(id);
            }
        }

        return result;
    }

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
            var pairwiseScores = new Dictionary<string, double>();
            var fingerprints = new Dictionary<long, uint[]>();

            foreach (var song in group)
            {
                var fp = await GetOrCreateFingerprintAsync(song, ct: ct);
                if (fp != null)
                {
                    fingerprints[song.Id] = BytesToUintArray(fp.Fingerprint);
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

            yield return (group, pairwiseScores);
        }
    }
}
