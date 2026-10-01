namespace MyMusic.Common.Services;

/// <summary>
/// An in-memory set of acoustic (Chromaprint) fingerprints that can be searched for soundalikes.
/// Candidates are found through a lookup table on the top 16 bits of each fingerprint value, and
/// then scored with <see cref="Compare"/>. Not thread-safe.
/// </summary>
public class FingerprintIndex<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, uint[]> _fingerprints = new();
    private readonly Dictionary<ushort, Dictionary<TKey, short>> _lookup = new();
    private long _totalLength;

    public IReadOnlyDictionary<TKey, uint[]> Fingerprints => _fingerprints;

    public int Count => _fingerprints.Count;

    public bool ContainsKey(TKey key) => _fingerprints.ContainsKey(key);

    public void Add(TKey key, uint[] fingerprint)
    {
        if (!_fingerprints.TryAdd(key, fingerprint))
            return;

        _totalLength += fingerprint.Length;

        foreach (var v in fingerprint)
        {
            var bucket = (ushort)(v >> 16);
            if (!_lookup.TryGetValue(bucket, out var counts))
            {
                counts = new Dictionary<TKey, short>();
                _lookup[bucket] = counts;
            }

            counts[key] = (short)(counts.GetValueOrDefault(key) + 1);
        }
    }

    /// <summary>
    /// Average fingerprint length of the index, used to derive the candidate threshold.
    /// </summary>
    public double AverageLength => _fingerprints.Count == 0 ? 0 : (double)_totalLength / _fingerprints.Count;

    /// <summary>
    /// Keys whose fingerprints share at least <paramref name="threshold"/> lookup buckets with
    /// <paramref name="fingerprint"/>, except those <paramref name="exclude"/> returns true for.
    /// </summary>
    public List<TKey> FindCandidates(uint[] fingerprint, int threshold, Predicate<TKey>? exclude = null)
    {
        var hits = new Dictionary<TKey, Dictionary<ushort, short>>();

        foreach (var v in fingerprint)
        {
            var bucket = (ushort)(v >> 16);
            if (!_lookup.TryGetValue(bucket, out var counts))
                continue;

            foreach (var (key, count) in counts)
            {
                if (exclude != null && exclude(key))
                    continue;

                if (!hits.TryGetValue(key, out var seen))
                {
                    seen = new Dictionary<ushort, short>();
                    hits[key] = seen;
                }

                var current = seen.GetValueOrDefault(bucket);
                if (current < count)
                {
                    seen[bucket] = (short)(current + 1);
                }
            }
        }

        var result = new List<TKey>();
        foreach (var (key, seen) in hits)
        {
            var total = seen.Values.Sum(v => (int)v);
            if (total >= threshold)
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    /// The best-scoring fingerprint of the index that matches <paramref name="fingerprint"/> with a
    /// score of at least <paramref name="matchThreshold"/>, or <c>null</c> when none does.
    /// </summary>
    public (TKey Key, double Score)? FindBestMatch(uint[] fingerprint, double lookupThreshold, double matchThreshold)
    {
        if (_fingerprints.Count == 0 || fingerprint.Length == 0)
            return null;

        // The threshold is derived from the average length of every fingerprint involved, the queried
        // one included, as when the whole library is scanned for duplicates
        var averageLength = (double)(_totalLength + fingerprint.Length) / (_fingerprints.Count + 1);
        var threshold = (int)(averageLength * lookupThreshold);

        (TKey Key, double Score)? best = null;
        foreach (var key in FindCandidates(fingerprint, threshold))
        {
            var (score, _, _) = Compare(fingerprint, _fingerprints[key], false);
            if (score >= matchThreshold && (best == null || score > best.Value.Score))
            {
                best = (key, score);
            }
        }

        return best;
    }

    /// <summary>
    /// Similarity of two fingerprints (0 to 1), at the offset where they align best.
    /// </summary>
    public static (double Score, int OffsetA, int OffsetB) Compare(uint[] a, uint[] b, bool minLength)
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
}

/// <summary>
/// Conversions between fingerprints and their storage format in <c>SongAcousticFingerprint.Fingerprint</c>.
/// </summary>
public static class FingerprintEncoding
{
    public static byte[] ToBytes(uint[] arr)
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

    public static uint[] FromBytes(byte[] bytes)
    {
        var arr = new uint[bytes.Length / 4];
        for (var i = 0; i < arr.Length; i++)
        {
            arr[i] = (uint)(bytes[i * 4] | (bytes[i * 4 + 1] << 8) | (bytes[i * 4 + 2] << 16) | (bytes[i * 4 + 3] << 24));
        }
        return arr;
    }
}
