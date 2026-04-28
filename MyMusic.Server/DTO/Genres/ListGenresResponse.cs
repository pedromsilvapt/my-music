using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Genres;

public record ListGenresResponse
{
    public required List<ListGenreItem> Genres { get; init; }
}

public record ListGenreItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public int SongsCount { get; init; }

    public static ListGenreItem FromEntity(Entities.Genre genre) =>
        new()
        {
            Id = genre.Id,
            Name = genre.Name,
            SongsCount = genre.Songs.Count,
        };
}
