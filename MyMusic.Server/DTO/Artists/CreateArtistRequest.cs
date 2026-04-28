namespace MyMusic.Server.DTO.Artists;

public record CreateArtistRequest
{
    public required string Name { get; init; }
}
