using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// A soundalike of an uploaded file: a library song (<see cref="SongId"/>), or a file uploaded earlier in the
/// same session (<see cref="UploadChecksum"/> and <see cref="UploadPath"/>), whose song is created at commit.
/// </summary>
public record SyncSoundalikeMatch(long? SongId, string? UploadChecksum, string? UploadPath, double Score);

/// <summary>
/// Progress of fingerprinting a session's library: <see cref="Processed"/> of the <see cref="Total"/> songs
/// that had no stored fingerprint. <see cref="Done"/> once every library fingerprint is loaded.
/// </summary>
public record SyncLibraryPreparation(int Total, int Processed, bool Done);

/// <summary>
/// Matches files uploaded in a sync session with <c>Deduplicate</c> against the user's library and the
/// session's earlier uploads, by acoustic fingerprint. See docs/development/sync.md, "Soundalike Deduplication".
/// </summary>
public interface ISyncSoundalikeMatcher
{
    /// <summary>
    /// Fingerprints the uploaded file at <paramref name="filePath"/> and finds its best soundalike: first among
    /// the session's uploads, then in the library. Library songs missing a stored fingerprint are fingerprinted
    /// (and their fingerprints saved) on the session's first lookup.
    /// <para>
    /// When nothing matches, the file is remembered, in memory only, as an upload of the session that will be
    /// created on the server (by <paramref name="checksum"/>, at <paramref name="devicePath"/>), so later uploads
    /// can match it. The lookup and the registration are atomic, so concurrent uploads of soundalikes cannot
    /// miss each other. Nothing about the uploaded file is saved. Returns null when the file has no soundalike
    /// or cannot be fingerprinted.
    /// </para>
    /// </summary>
    Task<SyncSoundalikeMatch?> MatchOrRegisterAsync(long sessionId, long ownerId, string filePath,
        string checksum, string devicePath, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the library fingerprints of the session ahead of its uploads, fingerprinting at most
    /// <paramref name="maxSongs"/> library songs without a stored fingerprint per call, so the client can show
    /// progress. Called repeatedly until <see cref="SyncLibraryPreparation.Done"/>. Optional: the first lookup
    /// of <see cref="MatchOrRegisterAsync"/> fingerprints whatever is still missing.
    /// </summary>
    Task<SyncLibraryPreparation> PrepareLibraryAsync(long sessionId, long ownerId, int maxSongs,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fingerprints a song the commit created from an upload, and saves it, so later sessions match it
    /// without fingerprinting it again. Failures are logged, never thrown.
    /// </summary>
    Task SaveSongFingerprintAsync(long songId, CancellationToken cancellationToken);

    /// <summary>Forgets the session's soundalike state.</summary>
    void EndSession(long sessionId);
}

public class SyncSoundalikeMatcher(
    MusicDbContext db,
    IFpcalcService fpcalc,
    AcousticFingerprintService fingerprintService,
    SyncSoundalikeSessionCache cache,
    IOptions<AuditConfig> config,
    ILogger<SyncSoundalikeMatcher> logger) : ISyncSoundalikeMatcher
{
    public async Task<SyncSoundalikeMatch?> MatchOrRegisterAsync(long sessionId, long ownerId, string filePath,
        string checksum, string devicePath, CancellationToken cancellationToken)
    {
        var result = await fpcalc.FingerprintAsync(filePath,
            AcousticFingerprintService.DefaultFingerprintLength,
            AcousticFingerprintService.DefaultFingerprintAlgorithm,
            cancellationToken);
        if (result == null || result.Fingerprint.Length == 0)
        {
            logger.LogDebug("Could not fingerprint uploaded file {FilePath}, skipping soundalike check", filePath);
            return null;
        }

        var fingerprint = result.Fingerprint;
        var lookupThreshold = config.Value.SoundalikeLookupThreshold;
        var matchThreshold = config.Value.SoundalikeMatchThreshold;

        var entry = cache.Get(sessionId);
        await entry.Lock.WaitAsync(cancellationToken);
        try
        {
            var uploadMatch = entry.Uploads.FindBestMatch(fingerprint, lookupThreshold, matchThreshold);
            if (uploadMatch != null)
            {
                var (uploadChecksum, score) = uploadMatch.Value;
                return new SyncSoundalikeMatch(null, uploadChecksum, entry.UploadPaths[uploadChecksum], score);
            }

            var library = await LoadLibraryAsync(entry, ownerId, int.MaxValue, cancellationToken);

            var libraryMatch = library.FindBestMatch(fingerprint, lookupThreshold, matchThreshold);
            if (libraryMatch != null)
            {
                var (songId, score) = libraryMatch.Value;
                return new SyncSoundalikeMatch(songId, null, null, score);
            }

            // A later file with the same checksum is linked by checksum, so the first upload stays the source
            if (!entry.Uploads.ContainsKey(checksum))
            {
                entry.Uploads.Add(checksum, fingerprint);
                entry.UploadPaths[checksum] = devicePath;
            }

            return null;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    public async Task<SyncLibraryPreparation> PrepareLibraryAsync(long sessionId, long ownerId, int maxSongs,
        CancellationToken cancellationToken)
    {
        var entry = cache.Get(sessionId);
        await entry.Lock.WaitAsync(cancellationToken);
        try
        {
            await LoadLibraryAsync(entry, ownerId, maxSongs, cancellationToken);

            var total = entry.LibrarySongsToFingerprint;
            var pending = entry.PendingLibrarySongIds.Count;
            return new SyncLibraryPreparation(total, total - pending, pending == 0);
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    public async Task SaveSongFingerprintAsync(long songId, CancellationToken cancellationToken)
    {
        try
        {
            var song = await db.Songs.FirstOrDefaultAsync(s => s.Id == songId, cancellationToken);
            if (song == null)
                return;

            var fingerprint = await fingerprintService.GetOrCreateFingerprintAsync(song, ct: cancellationToken);
            if (fingerprint == null)
            {
                logger.LogWarning("Could not fingerprint song {SongId} created by sync", songId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to save the fingerprint of song {SongId} created by sync", songId);
        }
    }

    public void EndSession(long sessionId) => cache.Remove(sessionId);

    /// <summary>
    /// Loads the session's library fingerprints, then fingerprints at most <paramref name="maxSongs"/> of the
    /// library songs still missing one. The caller must hold the entry's lock.
    /// </summary>
    private async Task<FingerprintIndex<long>> LoadLibraryAsync(SyncSoundalikeSessionEntry entry, long ownerId,
        int maxSongs, CancellationToken cancellationToken)
    {
        if (entry.Library == null)
        {
            entry.Library = await LoadStoredLibraryAsync(entry, ownerId, cancellationToken);
        }

        var hadPending = entry.PendingLibrarySongIds.Count > 0;
        for (var i = 0; i < maxSongs && entry.PendingLibrarySongIds.TryPeek(out var songId); i++)
        {
            var song = await db.Songs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == songId, cancellationToken);
            var fingerprint = song == null
                ? null
                : await fingerprintService.GetOrCreateFingerprintAsync(song, ct: cancellationToken);
            if (fingerprint != null)
            {
                entry.Library.Add(songId, FingerprintEncoding.FromBytes(fingerprint.Fingerprint));
            }

            // Dequeued only once processed, so a cancelled request leaves the song for the next one
            entry.PendingLibrarySongIds.Dequeue();
        }

        if (hadPending && entry.PendingLibrarySongIds.Count == 0)
        {
            logger.LogInformation("Loaded {Count} library fingerprints of user {OwnerId} for soundalike deduplication",
                entry.Library.Count, ownerId);
        }

        return entry.Library;
    }

    /// <summary>
    /// Loads the stored fingerprints of every song of the owner in a single query, and queues the songs missing
    /// one in <see cref="SyncSoundalikeSessionEntry.PendingLibrarySongIds"/>.
    /// </summary>
    private async Task<FingerprintIndex<long>> LoadStoredLibraryAsync(SyncSoundalikeSessionEntry entry, long ownerId,
        CancellationToken cancellationToken)
    {
        var songs = await db.Songs
            .Where(s => s.OwnerId == ownerId)
            .Select(s => new { s.Id, s.Checksum, s.ChecksumAlgorithm })
            .ToListAsync(cancellationToken);

        var stored = (await db.SongAcousticFingerprints
                .Where(f => f.OwnerId == ownerId
                            && f.FingerprintAlgorithm == AcousticFingerprintService.DefaultFingerprintAlgorithm)
                .Select(f => new { f.Checksum, f.ChecksumAlgorithm, f.FingerprintLength, f.Fingerprint })
                .ToListAsync(cancellationToken))
            .Where(f => Math.Abs(f.FingerprintLength - AcousticFingerprintService.DefaultFingerprintLength) < 0.001)
            .GroupBy(f => (f.Checksum, f.ChecksumAlgorithm))
            .ToDictionary(g => g.Key, g => g.First().Fingerprint);

        var index = new FingerprintIndex<long>();

        foreach (var song in songs)
        {
            if (stored.TryGetValue((song.Checksum, song.ChecksumAlgorithm), out var bytes))
            {
                index.Add(song.Id, FingerprintEncoding.FromBytes(bytes));
            }
            else
            {
                entry.PendingLibrarySongIds.Enqueue(song.Id);
            }
        }

        entry.LibrarySongsToFingerprint = entry.PendingLibrarySongIds.Count;

        if (entry.LibrarySongsToFingerprint > 0)
        {
            logger.LogInformation("Fingerprinting {Count} songs of user {OwnerId} for soundalike deduplication",
                entry.LibrarySongsToFingerprint, ownerId);
        }
        else
        {
            logger.LogInformation("Loaded {Count} library fingerprints of user {OwnerId} for soundalike deduplication",
                index.Count, ownerId);
        }

        return index;
    }
}
