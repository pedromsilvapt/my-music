namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Request to apply auto-fetched metadata to a song.
/// </summary>
public record ApplyMetadataRequest
{
    /// <summary>
    /// Optional list of field names that were applied. 
    /// If empty, all pending metadata is marked as applied.
    /// </summary>
    public List<string> AppliedFields { get; set; } = [];
}
