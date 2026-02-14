using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Purchases;

public record PurchasedSongDataDto { }

public record PurchasedSongDto
{
    public required long Id { get; set; }

    public required long SourceId { get; set; }
    public required string SourceName { get; set; }
    public required string SourceIcon { get; set; }

    public required long UserId { get; set; }

    public required string ExternalId { get; set; }

    public string? Cover { get; set; }

    public required string Title { get; set; }

    public required string SubTitle { get; set; }

    public required Entities.PurchasedSongStatus Status { get; set; }

    public required int Progress { get; set; }

    public long? SongId { get; set; }

    public required string? ErrorMessage { get; set; }

    public required DateTime CreatedAt { get; set; }

    public static PurchasedSongDto FromEntity(Entities.PurchasedSong purchasedSong) =>
        Mapper.Map(purchasedSong).ToANew<PurchasedSongDto>();
}