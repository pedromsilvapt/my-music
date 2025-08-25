using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(SongId), nameof(GenreId), IsUnique = true)]
public class SongGenre
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }
    
    public Genre Genre { get; set; } = null!;
    public long GenreId { get; set; }
}
