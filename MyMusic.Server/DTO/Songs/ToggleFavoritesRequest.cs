namespace MyMusic.Server.DTO.Songs;

public record ToggleFavoritesRequest
{
    public required List<long> Ids { get; init; }
}

public record ToggleFavoritesResponse
{
    public required List<ToggleFavoriteItem> Songs { get; init; }
}

public record ToggleFavoriteItem
{
    public required long Id { get; init; }
    public required bool IsFavorite { get; init; }
}