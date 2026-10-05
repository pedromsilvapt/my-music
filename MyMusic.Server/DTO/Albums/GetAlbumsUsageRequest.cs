namespace MyMusic.Server.DTO.Albums;

public record GetAlbumsUsageRequest
{
    public required List<long> AlbumIds { get; init; }
}
