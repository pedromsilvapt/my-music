namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response after clearing all metadata fetch tasks and auto-fetched metadata.
/// </summary>
public record ClearAllTasksResponse
{
    /// <summary>
    /// Number of metadata fetch tasks deleted.
    /// </summary>
    public required int TasksDeleted { get; set; }

    /// <summary>
    /// Number of auto-fetched metadata records deleted.
    /// </summary>
    public required int MetadataDeleted { get; set; }
}
