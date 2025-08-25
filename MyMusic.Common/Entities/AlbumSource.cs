using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class AlbumSource
{
    public long Id { get; set; }

    public Album Album { get; set; } = null!;
    public long AlbumId { get; set; }

    public Source Source { get; set; } = null!;
    public long SourceId { get; set; }

    [MaxLength(256)]
    public required string ExternalId { get; set; }
    
    public required int SongsCount { get; set; }
}