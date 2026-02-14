using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record DeletePurchaseResponse
{
    public required DeletePurchaseItem Purchase { get; set; }
}

public record DeletePurchaseItem : PurchasedSongDto
{
    public new static DeletePurchaseItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<DeletePurchaseItem>();
}