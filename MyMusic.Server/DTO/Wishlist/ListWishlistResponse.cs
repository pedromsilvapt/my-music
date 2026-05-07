namespace MyMusic.Server.DTO.Wishlist;

public record ListWishlistResponse
{
    public required List<WishlistItem> Items { get; init; }
}