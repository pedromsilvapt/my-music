namespace MyMusic.Server.DTO.Wishlist;

public record UpdateWishlistResponse
{
    public required WishlistItem Item { get; init; }
}