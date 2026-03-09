namespace MyMusic.Server.DTO.Artwork;

public record GetArtworkMetadataResponse
{
    public required long Id { get; init; }
    public required string MimeType { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    public static GetArtworkMetadataResponse FromEntity(Common.Entities.Artwork artwork) =>
        new()
        {
            Id = artwork.Id,
            MimeType = artwork.MimeType,
            Width = artwork.Width,
            Height = artwork.Height,
        };
}