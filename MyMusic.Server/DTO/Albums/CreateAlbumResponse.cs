namespace MyMusic.Server.DTO.Albums;

public record CreateAlbumResponse
{
    public required CreateAlbumItem Album { get; init; }
}

public record CreateAlbumItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public int? Year { get; init; }
    public required long ArtistId { get; init; }
}
