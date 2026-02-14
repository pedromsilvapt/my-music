using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record RequeuePurchaseResponse
{
    public required RequeuePurchaseItem Purchase { get; set; }
}

public record RequeuePurchaseItem : PurchasedSongDto
{
    public new static RequeuePurchaseItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<RequeuePurchaseItem>();
}