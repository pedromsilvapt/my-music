namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response after requeuing failed metadata fetch tasks.
/// </summary>
public record RequeueFailedMetadataResponse
{
    /// <summary>
    /// Number of tasks successfully requeued.
    /// </summary>
    public required int RequeuedCount { get; set; }

    /// <summary>
    /// Number of tasks that failed to requeue.
    /// </summary>
    public required int FailedCount { get; set; }
}
