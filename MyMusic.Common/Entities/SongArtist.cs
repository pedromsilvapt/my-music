using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(SongId), nameof(ArtistId), IsUnique = true)]
public class SongArtist
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public Artist Artist { get; set; } = null!;
    public long ArtistId { get; set; }
}
