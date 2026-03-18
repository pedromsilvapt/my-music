namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Request to trigger batch metadata fetch for eligible songs.
/// </summary>
public record BatchMetadataFetchRequest
{
    /// <summary>
    /// Optional: Limit to specific songs (defaults to all eligible songs for current user).
    /// </summary>
    public List<long>? SongIds { get; set; }
}
