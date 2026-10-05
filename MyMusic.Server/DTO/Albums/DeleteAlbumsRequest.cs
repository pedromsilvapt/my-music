namespace MyMusic.Server.DTO.Albums;

public record DeleteAlbumsRequest
{
    public required List<long> AlbumIds { get; init; }
}
