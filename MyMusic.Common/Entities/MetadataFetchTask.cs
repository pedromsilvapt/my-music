using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

/// <summary>
/// Background task queue entity for metadata fetching operations.
/// Mirrors the PurchasedSong pattern for background queue processing.
/// </summary>
public class MetadataFetchTask
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public required MetadataFetchStatus Status { get; set; } = MetadataFetchStatus.Queued;

    public required int Progress { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public MetadataFetchFailureReason FailureReason { get; set; } = MetadataFetchFailureReason.None;

    public required DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public enum MetadataFetchStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>
/// Categorized failure reasons for metadata fetch tasks.
/// </summary>
public enum MetadataFetchFailureReason
{
    None = 0,
    ServiceUnavailable = 1,
    NoMetadataFound = 2,
    NetworkError = 3,
    SystemError = 4,
    Timeout = 5
}
