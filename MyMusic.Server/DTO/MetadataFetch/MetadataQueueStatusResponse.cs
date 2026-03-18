namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response containing the current status of the metadata fetch queue.
/// </summary>
public record MetadataQueueStatusResponse
{
    /// <summary>
    /// Number of tasks waiting to be processed.
    /// </summary>
    public required int Queued { get; set; }

    /// <summary>
    /// Number of tasks currently being processed.
    /// </summary>
    public required int Processing { get; set; }

    /// <summary>
    /// Number of successfully completed tasks.
    /// </summary>
    public required int Completed { get; set; }

    /// <summary>
    /// Number of failed tasks.
    /// </summary>
    public required int Failed { get; set; }

    /// <summary>
    /// Total number of tasks (sum of all statuses).
    /// </summary>
    public required int Total { get; set; }

    /// <summary>
    /// Estimated completion time for remaining tasks.
    /// </summary>
    public DateTime? EstimatedCompletion { get; set; }
}
