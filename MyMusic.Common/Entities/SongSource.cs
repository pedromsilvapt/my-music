using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class SongSource
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public Source Source { get; set; } = null!;
    public long SourceId { get; set; }

    [MaxLength(256)] public required string ExternalId { get; set; }

    public string? Link { get; set; } = null;

    public decimal Price { get; set; } = 0;
}