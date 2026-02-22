using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;

namespace MyMusic.Common.Entities;

public class Song
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Title { get; set; }

    [MaxLength(256)] public required string Label { get; set; }

    public Album Album { get; set; } = null!;
    public long AlbumId { get; set; }

    public Artwork? Cover { get; set; }
    public long? CoverId { get; set; }

    public int? Year { get; set; }

    [MaxLength(65536)] public string? Lyrics { get; set; }

    public bool Explicit { get; set; }

    public long Size { get; set; }

    public int? Track { get; set; }

    public TimeSpan Duration { get; set; }

    public required User Owner { get; set; }
    public long OwnerId { get; set; }

    public decimal? Rating { get; set; }

    public bool IsFavorite { get; set; }

    [MaxLength(1024)] public required string RepositoryPath { get; set; }

    [MaxLength(88)] public required string Checksum { get; set; }

    [MaxLength(64)] public required string ChecksumAlgorithm { get; set; }

    public required DateTime? AddedAt { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required DateTime ModifiedAt { get; set; }

    public required List<SongArtist> Artists { get; set; }

    public required List<SongGenre> Genres { get; set; }

    public required List<SongDevice> Devices { get; set; }

    public required List<SongSource> Sources { get; set; } = [];

    public List<PlaylistSong> PlaylistSongs { get; set; } = [];

    [Projectable] public int DurationSeconds => (int)Duration.TotalSeconds;

    [Projectable]
    public string DurationCategory =>
        Duration.TotalMinutes < 3 ? "Short" : Duration.TotalMinutes < 6 ? "Medium" : "Long";

    [Projectable] public bool HasLyrics => Lyrics != null && Lyrics != "";

    [Projectable] public int DaysSinceAdded => (int)(DateTime.UtcNow - (AddedAt ?? CreatedAt)).TotalDays;

    [Projectable] public int ArtistCount => Artists.Count;

    [Projectable] public int GenreCount => Genres.Count;

    [Projectable] public string SearchableText => (Title ?? "") + " " + (Album.Name ?? "") + " " + (Label ?? "");
}