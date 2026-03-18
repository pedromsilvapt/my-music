namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Request to requeue failed metadata fetch tasks.
/// </summary>
public record RequeueFailedMetadataRequest
{
    /// <summary>
    /// IDs of failed tasks to requeue. If empty, all failed tasks are requeued.
    /// </summary>
    public List<long> TaskIds { get; set; } = [];
}
