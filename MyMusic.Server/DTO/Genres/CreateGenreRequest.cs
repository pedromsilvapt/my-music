namespace MyMusic.Server.DTO.Genres;

public record CreateGenreRequest
{
    public required string Name { get; init; }
}
