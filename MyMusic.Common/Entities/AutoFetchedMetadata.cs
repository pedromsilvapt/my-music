using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MyMusic.Common.Entities;

/// <summary>
/// Stores raw source metadata (SourceSong) fetched from external sources for songs with audit issues.
/// The diff is constructed at runtime using MetadataDiffBuilder.
/// </summary>
public class AutoFetchedMetadata
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    /// <summary>
    /// The raw source metadata (SourceSong structure) stored as JSON.
    /// The metadata diff is constructed at runtime from this raw data.
    /// </summary>
    public required JsonElement SourceMetadata { get; set; }

    public required AutoFetchStatus Status { get; set; } = AutoFetchStatus.Pending;

    public Source? Source { get; set; }
    public long? SourceId { get; set; }

    public required DateTime FetchedAt { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
}

public enum AutoFetchStatus
{
    Pending = 0,
    Applied = 1,
    Failed = 2,
    Expired = 3
}
