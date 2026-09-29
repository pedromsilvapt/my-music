using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Queries over <see cref="SongChecksum"/>, the checksums a song's file has had over time.
/// </summary>
public static class SongChecksumHistory
{
    /// <summary>
    /// Whether <paramref name="checksum"/> (computed with <paramref name="checksumAlgorithm"/>) belongs to an older
    /// version of the song's file: it is in the song's checksum history. The current checksum is never a previous
    /// version, even if the file went back to it.
    /// </summary>
    public static async Task<bool> IsPreviousVersionAsync(MusicDbContext db, Song song, string? checksum,
        string checksumAlgorithm, CancellationToken cancellationToken = default)
    {
        if (checksum == null || (checksum == song.Checksum && checksumAlgorithm == song.ChecksumAlgorithm))
        {
            return false;
        }

        return await db.SongChecksums.AnyAsync(sc => sc.SongId == song.Id &&
                                                     sc.ChecksumAlgorithm == checksumAlgorithm &&
                                                     sc.Checksum == checksum, cancellationToken);
    }
}
