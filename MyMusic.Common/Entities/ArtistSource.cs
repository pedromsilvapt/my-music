using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class ArtistSource
{
    public long Id { get; set; }

    public Artist Artist { get; set; } = null!;
    public long ArtistId { get; set; }

    public Source Source { get; set; } = null!;
    public long SourceId { get; set; }

    [MaxLength(256)]
    public required string ExternalId { get; set; }
}