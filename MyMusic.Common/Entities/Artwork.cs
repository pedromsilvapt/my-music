using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class Artwork
{
    public long Id { get; set; }

    public required byte[] Data { get; set; }

    [MaxLength(256)]
    public required string MimeType { get; set; }

    public required int Width { get; set; }

    public required int Height { get; set; }
}
