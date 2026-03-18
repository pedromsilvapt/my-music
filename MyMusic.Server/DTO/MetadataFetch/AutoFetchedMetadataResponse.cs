using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.DTO.MetadataFetch;

/// <summary>
/// Response containing auto-fetched metadata for a song.
/// </summary>
public record AutoFetchedMetadataResponse
{
    /// <summary>
    /// Whether metadata exists for this song.
    /// </summary>
    public required bool HasMetadata { get; set; }

    /// <summary>
    /// The metadata diff if available.
    /// </summary>
    public SongMetadataDiff? Metadata { get; set; }

    /// <summary>
    /// When the metadata was fetched.
    /// </summary>
    public DateTime? FetchedAt { get; set; }

    /// <summary>
    /// Name of the source that provided the data.
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// Fields to pre-check based on audit rules.
    /// </summary>
    public List<string> PreSelectedFields { get; set; } = [];
}
