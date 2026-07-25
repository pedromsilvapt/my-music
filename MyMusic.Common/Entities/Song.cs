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

    public int? Bitrate { get; set; }

    public required User Owner { get; set; }
    public long OwnerId { get; set; }

    public decimal? Rating { get; set; }

    public bool IsFavorite { get; set; }

    public int PlayCount { get; set; }

    [MaxLength(1024)] public required string RepositoryPath { get; set; }

    [MaxLength(88)] public required string Checksum { get; set; }

    [MaxLength(64)] public required string ChecksumAlgorithm { get; set; }

    /// <summary>
    /// Represents when this song was added to this database (when the row was created
    /// </summary>
    public required DateTime? AddedAt { get; set; }

    /// <summary>
    /// Represents the very first date when the song was created. Should never change.
    /// In any kind of "merge"-like operation, the earliest of these dates should always prevail
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Must be updated every time any field on this entity changes
    /// </summary>
    public required DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Must be updated only when the file checksum changes
    /// </summary>
    // TODO: Make FileModifiedAt non-nullable once the backfill migration has run everywhere
    public DateTime? FileModifiedAt { get; set; }

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

    [Projectable] public string SearchableText => (Label ?? "") + " " + (Album.Name ?? "");
}
