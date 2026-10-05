namespace MyMusic.Server.DTO.Artists;

public record DeleteArtistsRequest
{
    public required List<long> ArtistIds { get; init; }
}
