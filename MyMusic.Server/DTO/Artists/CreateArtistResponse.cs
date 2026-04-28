namespace MyMusic.Server.DTO.Artists;

public record CreateArtistResponse
{
    public required CreateArtistItem Artist { get; init; }
}

public record CreateArtistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
}
