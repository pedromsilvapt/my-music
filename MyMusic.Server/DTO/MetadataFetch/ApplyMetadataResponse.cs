namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response after applying auto-fetched metadata.
/// </summary>
public record ApplyMetadataResponse
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// Status message describing the result.
    /// </summary>
    public required string Message { get; set; }
}
