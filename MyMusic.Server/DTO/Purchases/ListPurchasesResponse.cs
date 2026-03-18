using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record ListPurchasesResponse
{
    public required IEnumerable<ListPurchaseItem> Purchases { get; set; }
}

public record ListPurchaseItem : PurchasedSongDto
{
    public new static ListPurchaseItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<ListPurchaseItem>();
}