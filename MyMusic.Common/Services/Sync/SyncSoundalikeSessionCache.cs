using System.Collections.Concurrent;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// In-memory soundalike state of the sync sessions running with <c>Deduplicate</c>: the fingerprints of the
/// user's library, and of the files uploaded in the session that will be created on the server. Upload
/// fingerprints are never persisted (a dry run must not save anything about them), so they only live here,
/// until the session ends or has been idle for <see cref="IdleTimeout"/>.
/// </summary>
public class SyncSoundalikeSessionCache(TimeProvider timeProvider)
{
    public SyncSoundalikeSessionCache() : this(TimeProvider.System)
    {
    }

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<long, SyncSoundalikeSessionEntry> _entries = new();

    /// <summary>
    /// Returns the session's entry, creating it when missing. Entries of other sessions idle for longer than
    /// <see cref="IdleTimeout"/> are evicted, so abandoned sessions do not leak.
    /// </summary>
    public SyncSoundalikeSessionEntry Get(long sessionId)
    {
        var now = timeProvider.GetUtcNow();
        EvictIdle(now);

        var entry = _entries.GetOrAdd(sessionId, _ => new SyncSoundalikeSessionEntry());
        entry.LastAccessedAt = now;
        return entry;
    }

    public bool Contains(long sessionId) => _entries.ContainsKey(sessionId);

    public void Remove(long sessionId)
    {
        if (_entries.TryRemove(sessionId, out var entry))
        {
            entry.Lock.Dispose();
        }
    }

    private void EvictIdle(DateTimeOffset now)
    {
        foreach (var (sessionId, entry) in _entries)
        {
            if (now - entry.LastAccessedAt > IdleTimeout)
            {
                Remove(sessionId);
            }
        }
    }
}

/// <summary>
/// Soundalike state of one sync session. Every access must hold <see cref="Lock"/>.
/// </summary>
public class SyncSoundalikeSessionEntry
{
    public SemaphoreSlim Lock { get; } = new(1, 1);

    public DateTimeOffset LastAccessedAt { get; set; }

    /// <summary>
    /// Fingerprints of the user's songs by song id, created with the stored fingerprints on the session's first
    /// preparation or lookup. Complete only once <see cref="PendingLibrarySongIds"/> is empty.
    /// </summary>
    public FingerprintIndex<long>? Library { get; set; }

    /// <summary>Library songs without a stored fingerprint, still to be fingerprinted into <see cref="Library"/>.</summary>
    public Queue<long> PendingLibrarySongIds { get; } = new();

    /// <summary>How many library songs had no stored fingerprint when <see cref="Library"/> was loaded.</summary>
    public int LibrarySongsToFingerprint { get; set; }

    /// <summary>Fingerprints of this session's new uploads, by the checksum of the uploaded file.</summary>
    public FingerprintIndex<string> Uploads { get; } = new();

    /// <summary>Device path of each upload in <see cref="Uploads"/>, by checksum.</summary>
    public Dictionary<string, string> UploadPaths { get; } = new();
}
