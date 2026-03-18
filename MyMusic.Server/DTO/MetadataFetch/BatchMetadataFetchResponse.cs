namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response from batch metadata fetch trigger.
/// </summary>
public record BatchMetadataFetchResponse
{
    /// <summary>
    /// Number of tasks queued for processing.
    /// </summary>
    public required int TasksCreated { get; set; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public required string Message { get; set; }
}
