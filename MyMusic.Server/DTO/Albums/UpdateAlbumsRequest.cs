namespace MyMusic.Server.DTO.Albums;

public record UpdateAlbumsRequest
{
    /// <summary>
    ///     The albums to edit, as a single operation: every one of them is saved, or none is.
    /// </summary>
    public required List<UpdateAlbumsItem> Albums { get; init; }
}

public record UpdateAlbumsItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public int? Year { get; init; }
}
