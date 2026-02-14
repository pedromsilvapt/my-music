using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record RequeueManyPurchasesResponse
{
    public required IEnumerable<RequeueManyPurchasesItem> Purchases { get; set; }
}

public record RequeueManyPurchasesItem : PurchasedSongDto
{
    public new static RequeueManyPurchasesItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<RequeueManyPurchasesItem>();
}