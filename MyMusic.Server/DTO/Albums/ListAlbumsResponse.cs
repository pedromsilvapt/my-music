using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Albums;

public record ListAlbumsResponse
{
    public required IEnumerable<ListAlbumItem> Albums { get; set; }
}

public record ListAlbumItem
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Name { get; set; }
    public required int? Year { get; set; }
    public required int? SongsCount { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static ListAlbumItem FromEntity(Entities.Album album)
    {
        return new ListAlbumItem
        {
            Id = album.Id,
            Cover = album.CoverId,
            Name = album.Name,
            Year = album.Year,
            SongsCount = album.SongsCount,
            CreatedAt = album.CreatedAt,
        };
    }
}