using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Artists;

public record ListArtistsResponse
{
    public required IEnumerable<ListArtistsItem> Artists { get; set; }
}

public record ListArtistsItem
{
    public required long Id { get; set; }
    public required long? Photo { get; set; }
    public required string Name { get; set; }
    public required int? AlbumsCount { get; set; }
    public required int? SongsCount { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static ListArtistsItem FromEntity(Entities.Artist artist)
    {
        return new ListArtistsItem
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