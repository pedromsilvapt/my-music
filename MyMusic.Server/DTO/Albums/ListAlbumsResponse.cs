using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Albums;

public record ListAlbumsResponse
{
    public required IEnumerable<ListAlbumsItem> Albums { get; set; }
}

public record ListAlbumsItem
{
    public required long Id { get; set; }
    public required long? Cover { get; set; }
    public required string Name { get; set; }
    public required int? Year { get; set; }
    public required int? SongsCount { get; set; }

    public static ListAlbumsItem FromEntity(Entities.Album album)
    {
        return new ListAlbumsItem
        {
            Id = album.Id,
            Cover = album.CoverId,
            Name = album.Name,
            Year = album.Year,
            SongsCount = album.SongsCount,
        };
    }
}