namespace MyMusic.Server.DTO.Genres;

public record CreateGenreResponse
{
    public required CreateGenreItem Genre { get; init; }
}

public record CreateGenreItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
}
