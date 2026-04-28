namespace MyMusic.Server.DTO.Albums;

public record CreateAlbumRequest
{
    public required string Name { get; init; }
    public required long ArtistId { get; init; }
    public int? Year { get; init; }
}
