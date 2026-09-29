using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

/// <summary>
/// A previous checksum of the song's file; the current one is only in <see cref="Song.Checksum"/>. Rows are
/// recorded by a PostgreSQL trigger whenever <see cref="Song.Checksum"/> changes (see the AddSongChecksumHistory
/// migration), so files holding an older version of a song can still be recognised as that song.
/// </summary>
public class SongChecksum
{
    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    [MaxLength(88)] public required string Checksum { get; set; }

    [MaxLength(64)] public required string ChecksumAlgorithm { get; set; }

    /// <summary>When this checksum stopped being the song's current one.</summary>
    public DateTime CreatedAt { get; set; }
}
