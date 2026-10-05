namespace MyMusic.Server.DTO.Artists;

public record UpdateArtistsRequest
{
    /// <summary>
    ///     The artists to edit, as a single operation: every one of them is saved, or none is.
    /// </summary>
    public required List<UpdateArtistsItem> Artists { get; init; }
}

public record UpdateArtistsItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
}
