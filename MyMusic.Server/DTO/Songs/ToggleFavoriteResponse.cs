namespace MyMusic.Server.DTO.Songs;

public record ToggleFavoriteResponse
{
    public required bool IsFavorite { get; init; }
}