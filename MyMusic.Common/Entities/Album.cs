using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(OwnerId), nameof(ArtistId), nameof(Name), IsUnique = true)]
public class Album
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    public Artwork? Cover { get; set; }
    public long? CoverId { get; set; }

    public int? Year { get; set; }

    public Artist Artist { get; set; } = null!;
    public long ArtistId { get; set; }

    public User Owner { get; set; } = null!;

    public long OwnerId { get; set; }

    public List<AlbumSource> Sources { get; set; } = [];

    public List<Song> Songs { get; set; } = [];

    public required int SongsCount { get; set; }

    public required DateTime CreatedAt { get; set; }

    [Projectable] public string SearchableText => (Name ?? "") + " " + (Artist.Name ?? "");

    [Projectable] public int TotalDurationSeconds => Songs.Sum(s => (int)s.Duration.TotalSeconds);
}