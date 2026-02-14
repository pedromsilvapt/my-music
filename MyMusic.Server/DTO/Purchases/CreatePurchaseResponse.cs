using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record CreatePurchaseResponse
{
    public required CreatePurchaseItem Purchase { get; set; }
}

public record CreatePurchaseItem : PurchasedSongDto
{
    public new static CreatePurchaseItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<CreatePurchaseItem>();
}