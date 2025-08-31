using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class Song
{
    public long Id { get; set; }

    [MaxLength(256)]
    public required string Title { get; set; }

    [MaxLength(256)]
    public required string Label { get; set; }

    public Album Album { get; set; } = null!;
    public long AlbumId { get; set; }

    public Artwork? Cover { get; set; }
    public long? CoverId { get; set; }

    public int? Year { get; set; }
    
    [MaxLength(65536)]
    public string? Lyrics { get; set; }

    public bool Explicit { get; set; }

    public long Size { get; set; }

    public int? Track { get; set; }

    public TimeSpan Duration { get; set; }

    public required User Owner { get; set; }
    public long OwnerId { get; set; }

    public decimal? Rating { get; set; }

    [MaxLength(1024)]
    public required string RepositoryPath { get; set; }

    [MaxLength(88)]
    public required string Checksum { get; set; }

    [MaxLength(64)]
    public required string ChecksumAlgorithm { get; set; }

    public required DateTime? AddedAt { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required DateTime ModifiedAt { get; set; }

    public required List<SongArtist> Artists { get; set; }

    public required List<SongGenre> Genres { get; set; }

    public required List<SongDevice> Devices { get; set; }

    public required List<SongSource> Sources { get; set; } = [];
}
