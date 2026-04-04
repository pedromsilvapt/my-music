using MyMusic.Common.Entities;
using Entities = MyMusic.Common.Entities;
using SourcesDto = MyMusic.Server.DTO.Sources;

namespace MyMusic.Server.DTO.Wishlist;

public record CreateWishlistResponse
{
    public required WishlistItem Item { get; init; }
}

public record WishlistItem
{
    public required long Id { get; init; }
    public required long SourceId { get; init; }
    public required string Query { get; init; }
    public string? Filter { get; init; }
    public required WishlistItemStatus Status { get; init; }
    public required int ContinuousFailedCount { get; init; }
    public required string? LastErrorMessage { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required WishlistSource Source { get; init; }

    public static WishlistItem FromEntity(Entities.WishlistItem item) =>
        new()
        {
            Id = item.Id,
            SourceId = item.SourceId,
            Query = item.Query,
            Filter = item.Filter,
            Status = item.Status,
            ContinuousFailedCount = item.ContinuousFailedCount,
            LastErrorMessage = item.LastErrorMessage,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            Source = WishlistSource.FromEntity(item.Source)
        };
}

public record WishlistSource
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }

    public static WishlistSource FromEntity(Entities.Source source) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Icon = source.Icon
        };
}