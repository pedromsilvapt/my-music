using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Artists;

public record ListArtistsResponse
{
    public required IEnumerable<ListArtistItem> Artists { get; set; }
}

public record ListArtistItem
{
    public required long Id { get; set; }
    public required long? Photo { get; set; }
    public required string Name { get; set; }
    public required int? AlbumsCount { get; set; }
    public required int? SongsCount { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static ListArtistItem FromEntity(Entities.Artist artist)
    {
        return new ListArtistItem
        {
            Id = artist.Id,
            Photo = artist.PhotoId,
            Name = artist.Name,
            AlbumsCount = artist.AlbumsCount,
            SongsCount = artist.SongsCount,
            CreatedAt = artist.CreatedAt,
        };
    }
}