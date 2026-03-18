using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response containing detailed information about a failed metadata fetch task.
/// </summary>
public record FailedTaskDetailResponse
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public required long TaskId { get; set; }

    /// <summary>
    /// Song ID that failed to fetch metadata.
    /// </summary>
    public required long SongId { get; set; }

    /// <summary>
    /// Song title for display purposes.
    /// </summary>
    public required string SongTitle { get; set; }

    /// <summary>
    /// Categorized failure reason.
    /// </summary>
    public required MetadataFetchFailureReason Reason { get; set; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// When the task failed.
    /// </summary>
    public required DateTime FailedAt { get; set; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }
}
