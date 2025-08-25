using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(OwnerId), nameof(Name), IsUnique = true)]
public class Genre
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    public List<SongGenre> Songs { get; set; } = [];

    public User Owner { get; set; } = null!;
    public long OwnerId { get; set; }
}