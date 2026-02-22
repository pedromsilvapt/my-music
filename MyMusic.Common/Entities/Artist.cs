using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;

namespace MyMusic.Common.Entities;

public class Artist
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    public Artwork? Photo { get; set; }
    public long? PhotoId { get; set; }

    public Artwork? Background { get; set; }
    public long? BackgroundId { get; set; }

    public List<Album> Albums { get; set; } = [];

    public List<SongArtist> Songs { get; set; } = [];

    public List<ArtistSource> Sources { get; set; } = [];

    public User Owner { get; set; } = null!;

    public long OwnerId { get; set; }

    public required int SongsCount { get; set; }

    public required int AlbumsCount { get; set; }

    public required DateTime CreatedAt { get; set; }

    [Projectable] public string SearchableText => Name ?? "";
}