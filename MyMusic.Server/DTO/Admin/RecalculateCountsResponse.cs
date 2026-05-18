using MyMusic.Common.Services;

namespace MyMusic.Server.DTO.Admin;

public record RecalculateCountsResponse
{
    public required int AlbumsUpdated { get; set; }
    public required int ArtistsSongsUpdated { get; set; }
    public required int ArtistsAlbumsUpdated { get; set; }
}

public static class RecalculateCountsResponseMapping
{
    public static RecalculateCountsResponse ToResponse(this RecalculateCountsResult result) => new()
    {
        AlbumsUpdated = result.AlbumsUpdated,
        ArtistsSongsUpdated = result.ArtistsSongsUpdated,
        ArtistsAlbumsUpdated = result.ArtistsAlbumsUpdated,
    };
}