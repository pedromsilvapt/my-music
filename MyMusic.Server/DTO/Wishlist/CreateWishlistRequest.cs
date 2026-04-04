namespace MyMusic.Server.DTO.Wishlist;

public record CreateWishlistRequest
{
    public required long SourceId { get; init; }
    public required string Query { get; init; }
    public string? Filter { get; init; }
}