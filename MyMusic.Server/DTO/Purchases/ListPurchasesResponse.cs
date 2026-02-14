using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record ListPurchasesResponse
{
    public required IEnumerable<ListPurchasesItem> Purchases { get; set; }
}

public record ListPurchasesItem : PurchasedSongDto
{
    public new static ListPurchasesItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<ListPurchasesItem>();
}