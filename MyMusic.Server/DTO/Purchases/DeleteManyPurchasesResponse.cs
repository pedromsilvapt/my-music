using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record DeleteManyPurchasesResponse
{
    public required IEnumerable<DeleteManyPurchasesItem> Purchases { get; set; }
}

public record DeleteManyPurchasesItem : PurchasedSongDto
{
    public new static DeleteManyPurchasesItem FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<DeleteManyPurchasesItem>();
}