namespace MyMusic.Server.DTO.Artists;

public record MergeArtistsRequest
{
    /// <summary>
    ///     The artist that is kept, and takes in the others.
    /// </summary>
    public required long TargetId { get; init; }

    /// <summary>
    ///     The artists merged into the target, as a single operation: every one of them is merged, or none is.
    /// </summary>
    public required List<long> SourceIds { get; init; }
}
