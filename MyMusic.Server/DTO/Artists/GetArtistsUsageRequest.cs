namespace MyMusic.Server.DTO.Artists;

public record GetArtistsUsageRequest
{
    public required List<long> ArtistIds { get; init; }
}
